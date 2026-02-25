using System;
using System.Collections.Generic;

namespace SmartAssistant.Gateway;

public sealed class WakeArbitrationService
{
	private readonly int _lockTtlMs;
	private readonly Dictionary<string, WakeLockRecord> _records = new(StringComparer.Ordinal);
	private readonly object _sync = new();

	public WakeArbitrationService(int lockTtlMs)
	{
		_lockTtlMs = Math.Clamp(lockTtlMs, 1000, 120000);
	}

	public WakeClaimResponse Claim(WakeClaimRequest request)
	{
		long now = NowMillis();
		string homeId = request.HomeId;
		string deviceId = request.DeviceId;

		lock (_sync)
		{
			WakeLockRecord? existing = GetValidRecord(homeId, now);
			if (existing is not null)
			{
				if (string.Equals(existing.DeviceId, deviceId, StringComparison.Ordinal))
				{
					WakeLockRecord refreshed = existing with
					{
						LastHeartbeatAt = now,
						ExpiresAt = now + _lockTtlMs
					};
					_records[homeId] = refreshed;

					return new WakeClaimResponse
					{
						Granted = true,
						HomeId = homeId,
						DeviceId = deviceId,
						WakeToken = refreshed.WakeToken,
						WakeId = refreshed.WakeId,
						Reason = "refreshed",
						ExpiresInMs = _lockTtlMs
					};
				}

				return new WakeClaimResponse
				{
					Granted = false,
					HomeId = homeId,
					DeviceId = deviceId,
					OwnerDeviceId = existing.DeviceId,
					WakeId = existing.WakeId,
					Reason = "already_claimed",
					ExpiresInMs = Remaining(existing.ExpiresAt, now)
				};
			}

			WakeLockRecord created = new(
				HomeId: homeId,
				DeviceId: deviceId,
				WakeToken: Guid.NewGuid().ToString("N"),
				WakeId: string.IsNullOrWhiteSpace(request.WakeId) ? Guid.NewGuid().ToString("N") : request.WakeId!,
				ClaimedAt: now,
				LastHeartbeatAt: now,
				ExpiresAt: now + _lockTtlMs
			);
			_records[homeId] = created;

			return new WakeClaimResponse
			{
				Granted = true,
				HomeId = homeId,
				DeviceId = deviceId,
				WakeToken = created.WakeToken,
				WakeId = created.WakeId,
				Reason = "granted",
				ExpiresInMs = _lockTtlMs
			};
		}
	}

	public WakeValidateResponse Validate(string homeId, string deviceId, string wakeToken, bool refresh)
	{
		long now = NowMillis();
		lock (_sync)
		{
			WakeLockRecord? existing = GetValidRecord(homeId, now);
			if (existing is null)
			{
				return new WakeValidateResponse
				{
					Valid = false,
					HomeId = homeId
				};
			}

			if (!string.Equals(existing.DeviceId, deviceId, StringComparison.Ordinal) ||
				!string.Equals(existing.WakeToken, wakeToken, StringComparison.Ordinal))
			{
				return new WakeValidateResponse
				{
					Valid = false,
					HomeId = homeId,
					OwnerDeviceId = existing.DeviceId,
					ExpiresInMs = Remaining(existing.ExpiresAt, now)
				};
			}

			if (refresh)
			{
				existing = existing with
				{
					LastHeartbeatAt = now,
					ExpiresAt = now + _lockTtlMs
				};
				_records[homeId] = existing;
				return new WakeValidateResponse
				{
					Valid = true,
					HomeId = homeId,
					OwnerDeviceId = deviceId,
					ExpiresInMs = _lockTtlMs
				};
			}

			return new WakeValidateResponse
			{
				Valid = true,
				HomeId = homeId,
				OwnerDeviceId = deviceId,
				ExpiresInMs = Remaining(existing.ExpiresAt, now)
			};
		}
	}

	public WakeReleaseResponse Release(WakeReleaseRequest request)
	{
		long now = NowMillis();
		lock (_sync)
		{
			WakeLockRecord? existing = GetValidRecord(request.HomeId, now);
			if (existing is null)
			{
				return new WakeReleaseResponse
				{
					Released = false,
					Reason = "not_found"
				};
			}

			if (!string.Equals(existing.DeviceId, request.DeviceId, StringComparison.Ordinal) ||
				!string.Equals(existing.WakeToken, request.WakeToken, StringComparison.Ordinal))
			{
				return new WakeReleaseResponse
				{
					Released = false,
					Reason = "owner_mismatch",
					OwnerDeviceId = existing.DeviceId
				};
			}

			_records.Remove(request.HomeId);
			return new WakeReleaseResponse
			{
				Released = true,
				Reason = "released"
			};
		}
	}

	public WakeArbitrationHealthSnapshot GetHealthSnapshot()
	{
		lock (_sync)
		{
			PruneExpired(NowMillis());
			return new WakeArbitrationHealthSnapshot
			{
				Backend = "in_process_memory",
				LockTtlMs = _lockTtlMs,
				ActiveLocks = _records.Count
			};
		}
	}

	private WakeLockRecord? GetValidRecord(string homeId, long now)
	{
		if (!_records.TryGetValue(homeId, out WakeLockRecord? record))
		{
			return null;
		}

		if (record.ExpiresAt <= now)
		{
			_records.Remove(homeId);
			return null;
		}

		return record;
	}

	private void PruneExpired(long now)
	{
		List<string>? toDelete = null;
		foreach (KeyValuePair<string, WakeLockRecord> pair in _records)
		{
			if (pair.Value.ExpiresAt > now)
			{
				continue;
			}

			toDelete ??= new List<string>();
			toDelete.Add(pair.Key);
		}

		if (toDelete is null)
		{
			return;
		}

		foreach (string key in toDelete)
		{
			_records.Remove(key);
		}
	}

	private static int Remaining(long expiresAt, long now)
	{
		long remaining = expiresAt - now;
		if (remaining <= 0)
		{
			return 0;
		}
		if (remaining >= int.MaxValue)
		{
			return int.MaxValue;
		}
		return (int)remaining;
	}

	private static long NowMillis()
	{
		return DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
	}
}

public sealed class WakeArbitrationHealthSnapshot
{
	public string Backend { get; init; } = string.Empty;
	public int LockTtlMs { get; init; }
	public int ActiveLocks { get; init; }
}

internal sealed record WakeLockRecord(
	string HomeId,
	string DeviceId,
	string WakeToken,
	string WakeId,
	long ClaimedAt,
	long LastHeartbeatAt,
	long ExpiresAt
);
