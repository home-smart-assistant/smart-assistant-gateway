using System;
using System.Collections.Concurrent;
using System.Net.Http;
using System.Net.Http.Json;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Builder;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.DependencyInjection;

namespace SmartAssistant.Gateway;

public class Program
{
	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions(JsonSerializerDefaults.Web)
	{
		PropertyNameCaseInsensitive = true
	};

	public static async Task Main(string[] args)
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
		builder.Services.AddEndpointsApiExplorer();
		builder.Services.AddSwaggerGen();
		builder.Services.AddHttpClient("agent", delegate(HttpClient client)
		{
			string uriString = builder.Configuration["Services:AgentBaseUrl"] ?? "http://localhost:8091";
			client.BaseAddress = new Uri(uriString);
		});
		builder.Services.AddHttpClient("haBridge", delegate(HttpClient client)
		{
			string uriString = builder.Configuration["Services:HomeAssistantBridgeBaseUrl"] ?? "http://localhost:8092";
			client.BaseAddress = new Uri(uriString);
		});
		WebApplication webApplication = builder.Build();
		webApplication.UseSwagger();
		webApplication.UseSwaggerUI();
		webApplication.UseWebSockets();
		ConcurrentDictionary<string, SessionState> sessions = new ConcurrentDictionary<string, SessionState>();
		webApplication.MapGet("/health", (Func<IHttpClientFactory, CancellationToken, Task<IResult>>)async delegate(IHttpClientFactory httpClientFactory, CancellationToken ct)
		{
			bool agentAlive = await ProbeAsync(httpClientFactory.CreateClient("agent"), ct);
			bool flag = await ProbeAsync(httpClientFactory.CreateClient("haBridge"), ct);
			return Results.Ok(new
			{
				service = "smart_assistant_gateway",
				status = "ok",
				downstream = new
				{
					agent = (agentAlive ? "ok" : "degraded"),
					haBridge = (flag ? "ok" : "degraded")
				},
				time = DateTimeOffset.UtcNow
			});
		});
		webApplication.MapPost("/session/start", (Func<SessionStartRequest, IResult>)(([FromBody] SessionStartRequest request) =>
		{
			string text = Guid.NewGuid().ToString("N");
			SessionState sessionState = new SessionState(text, request.DeviceId, DateTimeOffset.UtcNow);
			sessions[text] = sessionState;
			return Results.Ok(new SessionStartResponse(text, sessionState.CreatedAt, "hybrid"));
		}));
		webApplication.MapPost("/turn/text", (Func<TurnTextRequest, IHttpClientFactory, CancellationToken, Task<IResult>>)(async ([FromBody] TurnTextRequest request, IHttpClientFactory httpClientFactory, CancellationToken ct) =>
		{
			if (string.IsNullOrWhiteSpace(request.Text))
			{
				return Results.BadRequest(new
				{
					error = "text is required"
				});
			}
			string sessionId = request.SessionId;
			if (string.IsNullOrWhiteSpace(sessionId))
			{
				sessionId = Guid.NewGuid().ToString("N");
			}
			sessions.AddOrUpdate(sessionId, (string _) => new SessionState(sessionId, request.DeviceId, DateTimeOffset.UtcNow), (string _, SessionState state) => state with
			{
				LastTurnAt = DateTimeOffset.UtcNow
			});
			AgentRespondResponse agentRespondResponse = await SendAgentRequestAsync(httpClientFactory, new AgentRespondRequest(sessionId, request.Text, request.Metadata), ct);
			return ((object)agentRespondResponse == null) ? Results.Ok(new TurnTextResponse(sessionId, "抱歉，Agent 服务暂时不可用。", null, "gateway_fallback")) : Results.Ok(new TurnTextResponse(agentRespondResponse.SessionId, agentRespondResponse.ReplyText, agentRespondResponse.ToolCall, agentRespondResponse.Source));
		}));
		webApplication.Map("/turn/stream", async delegate(HttpContext context)
		{
			if (!context.WebSockets.IsWebSocketRequest)
			{
				context.Response.StatusCode = 400;
				await context.Response.WriteAsJsonAsync(new
				{
					error = "WebSocket request required"
				});
			}
			else
			{
				await HandleWebSocketSessionAsync(await context.WebSockets.AcceptWebSocketAsync(), context.RequestServices.GetRequiredService<IHttpClientFactory>(), context.RequestAborted);
			}
		});
		webApplication.MapPost("/tool/call", (Func<ToolCallRequest, IHttpClientFactory, CancellationToken, Task<IResult>>)(async ([FromBody] ToolCallRequest request, IHttpClientFactory httpClientFactory, CancellationToken ct) =>
		{
			if (string.IsNullOrWhiteSpace(request.ToolName))
			{
				return Results.BadRequest(new
				{
					error = "tool_name is required"
				});
			}
			HttpClient client = httpClientFactory.CreateClient("haBridge");
			using HttpResponseMessage resp = await client.PostAsJsonAsync("/v1/tools/call", request, ct);
			string json = await resp.Content.ReadAsStringAsync(ct);
			if (!resp.IsSuccessStatusCode)
			{
				return Results.StatusCode(502);
			}
			ToolCallResult value = JsonSerializer.Deserialize<ToolCallResult>(json, JsonOptions) ?? new ToolCallResult(Success: false, "Bridge returned invalid payload", null, null);
			return Results.Ok(value);
		}));
		await webApplication.RunAsync();
	}

	private static async Task HandleWebSocketSessionAsync(WebSocket socket, IHttpClientFactory httpClientFactory, CancellationToken ct)
	{
		byte[] buffer = new byte[8192];
		while (!ct.IsCancellationRequested && socket.State == WebSocketState.Open)
		{
			WebSocketReceiveResult webSocketReceiveResult = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
			if (webSocketReceiveResult.MessageType == WebSocketMessageType.Close)
			{
				await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closed", ct);
				break;
			}
			string json = Encoding.UTF8.GetString(buffer, 0, webSocketReceiveResult.Count);
			TurnStreamMessage turnStreamMessage = JsonSerializer.Deserialize<TurnStreamMessage>(json, JsonOptions);
			if ((object)turnStreamMessage == null || string.IsNullOrWhiteSpace(turnStreamMessage.Text))
			{
				await SendWebSocketJsonAsync(socket, new
				{
					error = "invalid message"
				}, ct);
				continue;
			}
			string sessionId = (string.IsNullOrWhiteSpace(turnStreamMessage.SessionId) ? Guid.NewGuid().ToString("N") : turnStreamMessage.SessionId);
			AgentRespondResponse agentRespondResponse = await SendAgentRequestAsync(httpClientFactory, new AgentRespondRequest(sessionId, turnStreamMessage.Text, turnStreamMessage.Metadata), ct);
			if ((object)agentRespondResponse == null)
			{
				await SendWebSocketJsonAsync(socket, new
				{
					session_id = sessionId,
					reply_text = "抱歉，Agent 服务暂时不可用。",
					source = "gateway_fallback"
				}, ct);
			}
			else
			{
				await SendWebSocketJsonAsync(socket, new
				{
					session_id = agentRespondResponse.SessionId,
					reply_text = agentRespondResponse.ReplyText,
					source = agentRespondResponse.Source,
					tool_call = agentRespondResponse.ToolCall
				}, ct);
			}
		}
	}

	private static async Task<AgentRespondResponse?> SendAgentRequestAsync(IHttpClientFactory httpClientFactory, AgentRespondRequest request, CancellationToken ct)
	{
		_ = 1;
		try
		{
			HttpClient client = httpClientFactory.CreateClient("agent");
			using HttpResponseMessage resp = await client.PostAsJsonAsync("/v1/agent/respond", request, ct);
			if (!resp.IsSuccessStatusCode)
			{
				return null;
			}
			return await resp.Content.ReadFromJsonAsync<AgentRespondResponse>(ct);
		}
		catch
		{
			return null;
		}
	}

	private static async Task SendWebSocketJsonAsync(WebSocket socket, object payload, CancellationToken ct)
	{
		string s = JsonSerializer.Serialize(payload, JsonOptions);
		byte[] bytes = Encoding.UTF8.GetBytes(s);
		await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, endOfMessage: true, ct);
	}

	private static async Task<bool> ProbeAsync(HttpClient client, CancellationToken ct)
	{
		try
		{
			using HttpResponseMessage httpResponseMessage = await client.GetAsync("/health", ct);
			return httpResponseMessage.IsSuccessStatusCode;
		}
		catch
		{
			return false;
		}
	}
}
