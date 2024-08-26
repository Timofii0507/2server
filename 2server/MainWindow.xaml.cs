using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

namespace _2server
{
    public partial class MainWindow : Window
    {
        private UdpClient udpServer;
        private ConcurrentDictionary<IPEndPoint, DateTime> connectedClients = new ConcurrentDictionary<IPEndPoint, DateTime>();
        private ConcurrentDictionary<IPEndPoint, int> clientRequests = new ConcurrentDictionary<IPEndPoint, int>();
        private readonly int maxRequestsPerHour = 10;
        private readonly int maxClients = 100;
        private readonly TimeSpan clientTimeout = TimeSpan.FromMinutes(10);
        private readonly string logFilePath = "server_log.txt";
        private CancellationTokenSource cts;

        public MainWindow()
        {
            InitializeComponent();
        }

        private async void StartServer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                cts = new CancellationTokenSource();
                udpServer = new UdpClient(new IPEndPoint(IPAddress.Parse("192.168.0.1"), 11000)); 

                LogToFile("Server started...");
                LogToListBox("Server started...");

                Task.Run(() => RemoveInactiveClients(cts.Token), cts.Token);

                while (!cts.Token.IsCancellationRequested)
                {
                    var receivedResult = await udpServer.ReceiveAsync();
                    var clientEndpoint = receivedResult.RemoteEndPoint;

                    if (connectedClients.Count >= maxClients)
                    {
                        LogToFile($"Max clients reached. Disconnecting {clientEndpoint}.");
                        LogToListBox($"Max clients reached. Disconnecting {clientEndpoint}.");
                        continue;
                    }

                    connectedClients[clientEndpoint] = DateTime.Now;

                    if (clientRequests.ContainsKey(clientEndpoint))
                    {
                        clientRequests[clientEndpoint]++;
                        if (clientRequests[clientEndpoint] > maxRequestsPerHour)
                        {
                            LogToFile($"Client {clientEndpoint} exceeded request limit.");
                            LogToListBox($"Client {clientEndpoint} exceeded request limit.");
                            continue;
                        }
                    }
                    else
                    {
                        clientRequests[clientEndpoint] = 1;
                    }

                    string products = Encoding.UTF8.GetString(receivedResult.Buffer);
                    LogRequest(clientEndpoint, products);

                    string recipe = GetRecipe(products);
                    byte[] recipeBytes = Encoding.UTF8.GetBytes(recipe);

                    await udpServer.SendAsync(recipeBytes, recipeBytes.Length, clientEndpoint);

                    string imagePath = GetRecipeImagePath(recipe);
                    if (File.Exists(imagePath))
                    {
                        await SendRecipeImage(imagePath, clientEndpoint);
                    }
                }
            }
            catch (Exception ex)
            {
                LogToFile($"Error: {ex.Message}");
                LogToListBox($"Error: {ex.Message}");
            }
        }

        private void StopServer_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                cts.Cancel();
                udpServer.Close();
                LogToFile("Server stopped.");
                LogToListBox("Server stopped.");
            }
            catch (Exception ex)
            {
                LogToFile($"Error stopping server: {ex.Message}");
                LogToListBox($"Error stopping server: {ex.Message}");
            }
        }

        private void RemoveInactiveClients(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested)
                {
                    var inactiveClients = new List<IPEndPoint>();
                    foreach (var client in connectedClients)
                    {
                        if (DateTime.Now - client.Value > clientTimeout)
                        {
                            inactiveClients.Add(client.Key);
                        }
                    }

                    foreach (var client in inactiveClients)
                    {
                        connectedClients.TryRemove(client, out _);
                        clientRequests.TryRemove(client, out _);
                        LogToFile($"Client {client} disconnected due to inactivity.");
                        LogToListBox($"Client {client} disconnected due to inactivity.");
                    }

                    Thread.Sleep(60000);
                }
            }
            catch (Exception ex)
            {
                LogToFile($"Error removing inactive clients: {ex.Message}");
                LogToListBox($"Error removing inactive clients: {ex.Message}");
            }
        }

        private string GetRecipe(string products)
        {
            if (products.Contains("Caesar"))
            {
                return "Caesar:romaine lettuce;quail eggs;chicken meat;bacon;cherry tomatoes;white bread or baguette;parmesan;salad dressing";
            }
            return "No recipe found for the given products.";
        }

        private string GetRecipeImagePath(string recipe)
        {
            if (recipe.Contains("Caesar"))
            {
                return @"C:\Users\Tymofii\source\repos\2server\2server\img\images.jpeg";
            }
            return @"C:\Users\Tymofii\source\repos\2server\2server\img\mistake-3085712_640.jpg";
        }

        private async Task SendRecipeImage(string imagePath, IPEndPoint clientEndpoint)
        {
            try
            {
                var image = new BitmapImage(new Uri(imagePath, UriKind.Absolute));
                var encoder = new PngBitmapEncoder();
                encoder.Frames.Add(BitmapFrame.Create(image));

                using (var stream = new MemoryStream())
                {
                    encoder.Save(stream);
                    byte[] imageBytes = stream.ToArray();
                    await udpServer.SendAsync(imageBytes, imageBytes.Length, clientEndpoint);
                }
            }
            catch (Exception ex)
            {
                LogToFile($"Error sending image: {ex.Message}");
                LogToListBox($"Error sending image: {ex.Message}");
            }
        }

        private void LogRequest(IPEndPoint client, string products)
        {
            string logEntry = $"{DateTime.Now}: {client} requested recipes for {products}";
            LogToFile(logEntry);
            LogToListBox(logEntry);
        }

        private void LogToFile(string message)
        {
            try
            {
                File.AppendAllText(logFilePath, message + Environment.NewLine);
            }
            catch (Exception ex)
            {
                LogToListBox($"Error writing to log file: {ex.Message}");
            }
        }

        private void LogToListBox(string message)
        {
            Dispatcher.Invoke(() =>
            {
                LogListBox.Items.Add(message);
            });
        }
    }
}