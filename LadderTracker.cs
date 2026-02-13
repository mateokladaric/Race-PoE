using System;
using System.Collections.Generic;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;
using System.Text.Json;

namespace RacePoE
{
	public class LadderEntry
	{
		public int Rank;
		public string CharacterName;
		public string ClassName;
		public int Level;
		public long Experience;
	}

	public class LadderResult
	{
		public LadderEntry Player;
		public LadderEntry PlayerBefore;
		public LadderEntry PlayerAfter;
	}

	public class LadderTracker
	{
		private const int PageSize = 200;
		private const int RefreshIntervalMs = 10000;
		private const int ConcurrentBatchSize = 4;
		private const int DelayBetweenBatchesMs = 600;
		private const int MaxRetries = 3;

		private readonly string _characterName;
		private readonly string _leagueName;
		private readonly HttpClient _client;

		private int _lastKnownOffset = -1;

		public LadderResult LastResult { get; private set; }
		public string LastError { get; private set; }
		public bool IsSearching { get; private set; }

		public LadderTracker(string characterName, string leagueName)
		{
			_characterName = characterName;
			_leagueName = leagueName;

			_client = new HttpClient();
			_client.DefaultRequestHeaders.Add("accept", "application/json, text/javascript, */*; q=0.01");
			_client.DefaultRequestHeaders.Add("accept-language", "en-US,en;q=0.9");
			_client.DefaultRequestHeaders.Add("x-requested-with", "XMLHttpRequest");
			_client.DefaultRequestHeaders.Add("User-Agent", "Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36");
		}

		public async Task RunLoop(CancellationToken ct)
		{
			while (!ct.IsCancellationRequested)
			{
				try
				{
					IsSearching = true;
					LastError = null;

					var result = await FindCharacter(ct);
					LastResult = result;
					IsSearching = false;
				}
				catch (OperationCanceledException)
				{
					break;
				}
				catch (Exception ex)
				{
					LastError = ex.Message;
					IsSearching = false;
				}

				try { await Task.Delay(RefreshIntervalMs, ct); }
				catch (OperationCanceledException) { break; }
			}
		}

		private async Task<LadderResult> FindCharacter(CancellationToken ct)
		{
			// If we have a cached offset, check nearby first (sequential, only 2-3 pages)
			if (_lastKnownOffset >= 0)
			{
				int searchStart = Math.Max(0, _lastKnownOffset - PageSize);
				for (int offset = searchStart; offset <= _lastKnownOffset + PageSize; offset += PageSize)
				{
					ct.ThrowIfCancellationRequested();
					var entries = await FetchPage(offset, ct);
					if (entries == null || entries.Count == 0)
						break;

					var result = FindInEntries(entries);
					if (result != null)
					{
						_lastKnownOffset = offset;
						return result;
					}
				}
			}

			// Full scan with concurrent batches
			int total = await FetchTotal(ct);
			for (int batchStart = 0; batchStart < total; batchStart += PageSize * ConcurrentBatchSize)
			{
				ct.ThrowIfCancellationRequested();

				var tasks = new List<Task<List<LadderEntry>>>();
				var offsets = new List<int>();

				for (int i = 0; i < ConcurrentBatchSize; i++)
				{
					int offset = batchStart + i * PageSize;
					if (offset >= total) break;
					offsets.Add(offset);
					tasks.Add(FetchPage(offset, ct));
				}

				var batchResults = await Task.WhenAll(tasks);

				for (int i = 0; i < batchResults.Length; i++)
				{
					if (batchResults[i] == null || batchResults[i].Count == 0)
						continue;

					var result = FindInEntries(batchResults[i]);
					if (result != null)
					{
						_lastKnownOffset = offsets[i];
						return result;
					}
				}

				await Task.Delay(DelayBetweenBatchesMs, ct);
			}

			_lastKnownOffset = -1;
			return null;
		}

		private LadderResult FindInEntries(List<LadderEntry> entries)
		{
			for (int i = 0; i < entries.Count; i++)
			{
				if (string.Equals(entries[i].CharacterName, _characterName, StringComparison.OrdinalIgnoreCase))
				{
					return new LadderResult
					{
						PlayerBefore = i > 0 ? entries[i - 1] : null,
						Player = entries[i],
						PlayerAfter = i < entries.Count - 1 ? entries[i + 1] : null
					};
				}
			}
			return null;
		}

		private async Task<string> FetchWithRetry(string url, CancellationToken ct)
		{
			int retries = 0;
			while (true)
			{
				ct.ThrowIfCancellationRequested();
				var response = await _client.GetAsync(url, ct);

				if (response.IsSuccessStatusCode)
					return await response.Content.ReadAsStringAsync();

				if (response.StatusCode == (HttpStatusCode)429 && retries < MaxRetries)
				{
					retries++;
					int retryDelay = 2000 * retries;
					if (response.Headers.RetryAfter?.Delta != null)
						retryDelay = (int)response.Headers.RetryAfter.Delta.Value.TotalMilliseconds + 500;
					await Task.Delay(retryDelay, ct);
					continue;
				}

				response.EnsureSuccessStatusCode();
			}
		}

		private async Task<int> FetchTotal(CancellationToken ct)
		{
			string url = BuildUrl(0, 1);
			string body = await FetchWithRetry(url, ct);
			using (var doc = JsonDocument.Parse(body))
			{
				if (doc.RootElement.TryGetProperty("total", out var totalEl))
					return totalEl.GetInt32();
				return 15000;
			}
		}

		private async Task<List<LadderEntry>> FetchPage(int offset, CancellationToken ct)
		{
			string url = BuildUrl(offset, PageSize);
			string body = await FetchWithRetry(url, ct);

			var result = new List<LadderEntry>();

			using (var doc = JsonDocument.Parse(body))
			{
				if (!doc.RootElement.TryGetProperty("entries", out var entriesEl))
					return result;

				foreach (var item in entriesEl.EnumerateArray())
				{
					if (!item.TryGetProperty("character", out var character))
						continue;

					result.Add(new LadderEntry
					{
						Rank = item.TryGetProperty("rank", out var rankEl) ? rankEl.GetInt32() : 0,
						CharacterName = character.TryGetProperty("name", out var nameEl) ? nameEl.GetString() : "",
						ClassName = character.TryGetProperty("class", out var classEl) ? classEl.GetString() : "",
						Level = character.TryGetProperty("level", out var levelEl) ? levelEl.GetInt32() : 0,
						Experience = character.TryGetProperty("experience", out var expEl) ? expEl.GetInt64() : 0
					});
				}
			}

			return result;
		}

		private string BuildUrl(int offset, int limit)
		{
			long timestamp = DateTimeOffset.UtcNow.ToUnixTimeMilliseconds();
			return $"https://www.pathofexile.com/api/ladders?offset={offset}&limit={limit}" +
				   $"&id={Uri.EscapeDataString(_leagueName)}&type=league&realm=pc&_={timestamp}";
		}
	}
}