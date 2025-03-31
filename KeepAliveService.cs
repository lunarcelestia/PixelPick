using System;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

public class KeepAliveService
{
    private readonly string _botUrl;
    private readonly HttpClient _httpClient;
    private readonly CancellationTokenSource _cancellationTokenSource;

    public KeepAliveService(string botUrl)
    {
        _botUrl = botUrl;
        _httpClient = new HttpClient();
        _cancellationTokenSource = new CancellationTokenSource();
    }

    public void Start()
    {
        Task.Run(async () => await KeepAliveLoop(_cancellationTokenSource.Token));
    }

    public void Stop()
    {
        _cancellationTokenSource.Cancel();
    }

    private async Task KeepAliveLoop(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            try
            {
                var response = await _httpClient.GetAsync(_botUrl);
                Console.WriteLine($"Keep-alive pinged {_botUrl}. Status code: {response.StatusCode}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Keep-alive failed: {ex.Message}");
            }

            await Task.Delay(TimeSpan.FromMinutes(5), cancellationToken);
        }
    }
}
