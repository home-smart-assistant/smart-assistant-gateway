using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
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
	private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
	{
		PropertyNameCaseInsensitive = true
	};

	public static async Task Main(string[] args)
	{
		WebApplicationBuilder builder = WebApplication.CreateBuilder(args);
		builder.Services.AddEndpointsApiExplorer();
		builder.Services.AddSwaggerGen();
		builder.Services.AddHttpClient("agent", client =>
		{
			string uriString = builder.Configuration["Services:AgentBaseUrl"] ?? "http://localhost:8091";
			client.BaseAddress = new Uri(uriString);
		});
		builder.Services.AddHttpClient("haBridge", client =>
		{
			string uriString = builder.Configuration["Services:HomeAssistantBridgeBaseUrl"] ?? "http://localhost:8092";
			client.BaseAddress = new Uri(uriString);
		});
		int wakeLockTtlMs = ParseWakeLockTtl(builder.Configuration["WakeArbitration:LockTtlMs"]);
		WakeArbitrationService wakeArbitration = new(wakeLockTtlMs);

		WebApplication app = builder.Build();
		app.UseSwagger();
		app.UseSwaggerUI();
		app.UseWebSockets();

		ConcurrentDictionary<string, SessionState> sessions = new();

		app.MapGet("/health", async (IHttpClientFactory httpClientFactory, CancellationToken ct) =>
		{
			bool agentAlive = await ProbeAsync(httpClientFactory.CreateClient("agent"), ct);
			bool haAlive = await ProbeAsync(httpClientFactory.CreateClient("haBridge"), ct);
			WakeArbitrationHealthSnapshot wakeHealth = wakeArbitration.GetHealthSnapshot();

			return Results.Ok(new
			{
				service = "smart_assistant_gateway",
				status = "ok",
				downstream = new
				{
					agent = agentAlive ? "ok" : "degraded",
					haBridge = haAlive ? "ok" : "degraded",
					wakeCoordinator = "ok"
				},
				wake_arbitration = new
				{
					mode = wakeHealth.Backend,
					lock_ttl_ms = wakeHealth.LockTtlMs,
					active_locks = wakeHealth.ActiveLocks
				},
				time = DateTimeOffset.UtcNow
			});
		});

		app.MapPost("/session/start", ([FromBody] SessionStartRequest request) =>
		{
			string sessionId = Guid.NewGuid().ToString("N");
			SessionState session = new(sessionId, request.DeviceId, DateTimeOffset.UtcNow);
			sessions[sessionId] = session;
			return Results.Ok(new SessionStartResponse(sessionId, session.CreatedAt, "hybrid"));
		});

		app.MapPost("/turn/text", async ([FromBody] TurnTextRequest request, IHttpClientFactory httpClientFactory, CancellationToken ct) =>
			await HandleTextTurnAsync(request, sessions, httpClientFactory, wakeArbitration, ct));

		app.MapPost("/api/assistant/text-turn", async ([FromBody] TurnTextRequest request, IHttpClientFactory httpClientFactory, CancellationToken ct) =>
			await HandleTextTurnAsync(request, sessions, httpClientFactory, wakeArbitration, ct));

		app.MapPost("/api/assistant/turn", async (HttpContext context, IHttpClientFactory httpClientFactory, CancellationToken ct) =>
		{
			if (!context.Request.HasFormContentType)
			{
				return Results.BadRequest(new { error = "multipart/form-data required" });
			}

			IFormCollection form = await context.Request.ReadFormAsync(ct);
			bool hasAudio = form.Files.Any(file => string.Equals(file.Name, "audio", StringComparison.OrdinalIgnoreCase));
			string debugText = form["text"].FirstOrDefault() ?? string.Empty;

			TurnTextRequest request = new()
			{
				SessionId = form["session_id"].FirstOrDefault(),
				DeviceId = form["device_id"].FirstOrDefault(),
				HomeId = form["home_id"].FirstOrDefault(),
				WakeToken = form["wake_token"].FirstOrDefault(),
				Text = string.IsNullOrWhiteSpace(debugText)
					? (hasAudio ? "语音输入（网关调试模式）" : "语音输入")
					: debugText,
				Metadata = new Dictionary<string, string>
				{
					["input_type"] = hasAudio ? "audio_multipart" : "form_text",
					["gateway_mode"] = "debug_bridge"
				}
			};

			return await HandleTextTurnAsync(request, sessions, httpClientFactory, wakeArbitration, ct);
		});

		app.Map("/turn/stream", async context =>
		{
			if (!context.WebSockets.IsWebSocketRequest)
			{
				context.Response.StatusCode = 400;
				await context.Response.WriteAsJsonAsync(new { error = "WebSocket request required" });
				return;
			}

			await HandleWebSocketSessionAsync(await context.WebSockets.AcceptWebSocketAsync(), context.RequestServices.GetRequiredService<IHttpClientFactory>(), context.RequestAborted);
		});

		app.MapPost("/tool/call", async ([FromBody] ToolCallRequest request, IHttpClientFactory httpClientFactory, CancellationToken ct) =>
		{
			if (string.IsNullOrWhiteSpace(request.ToolName))
			{
				return Results.BadRequest(new { error = "tool_name is required" });
			}

			HttpClient client = httpClientFactory.CreateClient("haBridge");
			using HttpResponseMessage resp = await client.PostAsJsonAsync("/v1/tools/call", request, ct);
			string json = await resp.Content.ReadAsStringAsync(ct);
			if (!resp.IsSuccessStatusCode)
			{
				return Results.StatusCode(502);
			}

			ToolCallResult payload = JsonSerializer.Deserialize<ToolCallResult>(json, JsonOptions)
				?? new ToolCallResult
				{
					Success = false,
					Message = "Bridge returned invalid payload",
					Data = null,
					TraceId = null
				};
			return Results.Ok(payload);
		});

		app.MapPost("/v1/wake/claim", ([FromBody] WakeClaimRequest request) =>
			Results.Ok(wakeArbitration.Claim(request)));

		app.MapPost("/v1/wake/heartbeat", ([FromBody] WakeHeartbeatRequest request) =>
			Results.Ok(wakeArbitration.Validate(request.HomeId, request.DeviceId, request.WakeToken, refresh: true)));

		app.MapPost("/v1/wake/validate", ([FromBody] WakeHeartbeatRequest request) =>
			Results.Ok(wakeArbitration.Validate(request.HomeId, request.DeviceId, request.WakeToken, refresh: false)));

		app.MapPost("/v1/wake/release", ([FromBody] WakeReleaseRequest request) =>
			Results.Ok(wakeArbitration.Release(request)));

		await app.RunAsync();
	}

	private static async Task<IResult> HandleTextTurnAsync(
		TurnTextRequest request,
		ConcurrentDictionary<string, SessionState> sessions,
		IHttpClientFactory httpClientFactory,
		WakeArbitrationService wakeArbitration,
		CancellationToken ct)
	{
		if (string.IsNullOrWhiteSpace(request.Text))
		{
			return Results.BadRequest(new { error = "text is required" });
		}

		if (!string.IsNullOrWhiteSpace(request.WakeToken))
		{
			if (string.IsNullOrWhiteSpace(request.HomeId) || string.IsNullOrWhiteSpace(request.DeviceId))
			{
				return Results.BadRequest(new { error = "home_id and device_id are required when wake_token is provided" });
			}

			bool wakeValid = ValidateWakeOwnership(wakeArbitration, request.HomeId, request.DeviceId, request.WakeToken, refresh: true);
			if (!wakeValid)
			{
				return Results.Conflict(new { error = "wake token invalid or claimed by another device" });
			}
		}

		string sessionId = string.IsNullOrWhiteSpace(request.SessionId) ? Guid.NewGuid().ToString("N") : request.SessionId;
		sessions.AddOrUpdate(
			sessionId,
			_ => new SessionState(sessionId, request.DeviceId, DateTimeOffset.UtcNow),
			(_, state) => state with { LastTurnAt = DateTimeOffset.UtcNow });

		Dictionary<string, string>? metadata = MergeMetadata(request.Metadata, request.HomeId, request.DeviceId, request.WakeToken);
		AgentRespondResponse? agentResp = await SendAgentRequestAsync(httpClientFactory, new AgentRespondRequest(sessionId, request.Text, metadata), ct);
		if (agentResp is null)
		{
			return Results.Ok(new TurnTextResponse(sessionId, "抱歉，Agent 服务暂时不可用。", null, "gateway_fallback"));
		}

		return Results.Ok(new TurnTextResponse(agentResp.SessionId, agentResp.ReplyText, agentResp.ToolCall, agentResp.Source));
	}

	private static Dictionary<string, string>? MergeMetadata(
		Dictionary<string, string>? metadata,
		string? homeId,
		string? deviceId,
		string? wakeToken)
	{
		Dictionary<string, string> merged = metadata is null
			? new Dictionary<string, string>()
			: new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase);

		if (!string.IsNullOrWhiteSpace(homeId))
		{
			merged["home_id"] = homeId;
		}

		if (!string.IsNullOrWhiteSpace(deviceId))
		{
			merged["device_id"] = deviceId;
		}

		if (!string.IsNullOrWhiteSpace(wakeToken))
		{
			merged["wake_token"] = wakeToken;
		}

		return merged.Count == 0 ? null : merged;
	}

	private static bool ValidateWakeOwnership(
		WakeArbitrationService wakeArbitration,
		string homeId,
		string deviceId,
		string wakeToken,
		bool refresh)
	{
		WakeValidateResponse response = wakeArbitration.Validate(homeId, deviceId, wakeToken, refresh);
		return response.Valid;
	}

	private static async Task HandleWebSocketSessionAsync(WebSocket socket, IHttpClientFactory httpClientFactory, CancellationToken ct)
	{
		byte[] buffer = new byte[8192];
		while (!ct.IsCancellationRequested && socket.State == WebSocketState.Open)
		{
			WebSocketReceiveResult receive = await socket.ReceiveAsync(new ArraySegment<byte>(buffer), ct);
			if (receive.MessageType == WebSocketMessageType.Close)
			{
				await socket.CloseAsync(WebSocketCloseStatus.NormalClosure, "closed", ct);
				break;
			}

			string json = Encoding.UTF8.GetString(buffer, 0, receive.Count);
			TurnStreamMessage? message = JsonSerializer.Deserialize<TurnStreamMessage>(json, JsonOptions);
			if (message is null || string.IsNullOrWhiteSpace(message.Text))
			{
				await SendWebSocketJsonAsync(socket, new { error = "invalid message" }, ct);
				continue;
			}

			string sessionId = string.IsNullOrWhiteSpace(message.SessionId) ? Guid.NewGuid().ToString("N") : message.SessionId;
			AgentRespondResponse? response = await SendAgentRequestAsync(httpClientFactory, new AgentRespondRequest(sessionId, message.Text, message.Metadata), ct);
			if (response is null)
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
					session_id = response.SessionId,
					reply_text = response.ReplyText,
					source = response.Source,
					tool_call = response.ToolCall
				}, ct);
			}
		}
	}

	private static async Task<AgentRespondResponse?> SendAgentRequestAsync(IHttpClientFactory httpClientFactory, AgentRespondRequest request, CancellationToken ct)
	{
		try
		{
			HttpClient client = httpClientFactory.CreateClient("agent");
			using HttpResponseMessage response = await client.PostAsJsonAsync("/v1/agent/respond", request, ct);
			if (!response.IsSuccessStatusCode)
			{
				return null;
			}
			return await response.Content.ReadFromJsonAsync<AgentRespondResponse>(ct);
		}
		catch
		{
			return null;
		}
	}

	private static int ParseWakeLockTtl(string? raw)
	{
		if (!int.TryParse(raw, out int parsed))
		{
			return 8000;
		}
		return Math.Clamp(parsed, 1000, 120000);
	}

	private static async Task SendWebSocketJsonAsync(WebSocket socket, object payload, CancellationToken ct)
	{
		string json = JsonSerializer.Serialize(payload, JsonOptions);
		byte[] bytes = Encoding.UTF8.GetBytes(json);
		await socket.SendAsync(new ArraySegment<byte>(bytes), WebSocketMessageType.Text, true, ct);
	}

	private static async Task<bool> ProbeAsync(HttpClient client, CancellationToken ct)
	{
		try
		{
			using HttpResponseMessage response = await client.GetAsync("/health", ct);
			return response.IsSuccessStatusCode;
		}
		catch
		{
			return false;
		}
	}
}
