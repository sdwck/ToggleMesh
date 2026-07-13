using Microsoft.Extensions.Hosting;
using ToggleMesh.Common.Contexts;
using ToggleMesh.SDK.Attributes;
using ToggleMesh.SDK.Clients;
using ToggleMesh.SDK.Models;

namespace ToggleMesh.ConsoleClient;

[ToggleMeshContext]
public partial struct AotUserContext
{
    public int UserId { get; set; }
    public string Email { get; set; }
    public string Country { get; set; }
    public string Role { get; set; }
}

public class UiConfig
{
    public string Theme { get; set; } = "light";
    public bool ShowSidebar { get; set; } = true;
    public int ItemsPerPage { get; set; } = 10;
}

public class PurchaseEventProperties
{
    public string ItemId { get; set; } = "";
    public string Category { get; set; } = "";
    public bool IsSubscription { get; set; }
}

public class ShowcaseWorker : BackgroundService
{
    private readonly IToggleMeshClient _client;

    public ShowcaseWorker(IToggleMeshClient client)
    {
        _client = client;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            Console.WriteLine("\n\n------------------------------------------------");
            Console.WriteLine($"--- ToggleMesh SDK Showcase ---");

            var identity = "user-123";
            var context = new AotUserContext 
            { 
                UserId = Random.Shared.Next(1, 100000), 
                Email = $"user{Random.Shared.Next(100)}@example.com", 
                Country = Random.Shared.NextDouble() > 0.5 ? "US" : "UK",
                Role = Random.Shared.NextDouble() > 0.8 ? "admin" : "user"
            };
            
            var user = new ToggleMeshUser<AotUserContext>(identity, context);

            Console.WriteLine("\n[1] Boolean Flag Evaluation (AOT Context)");
            var isNewCheckoutEnabled = _client.IsEnabled("new-checkout", ref user);
            Console.WriteLine($"Flag 'new-checkout' -> {isNewCheckoutEnabled}");

            Console.WriteLine("\n[2] String Variation Evaluation");
            var buttonColor = _client.GetStringVariation(
                "button-color", 
                identity, 
                context, 
                "blue");
            Console.WriteLine($"Flag 'button-color' -> {buttonColor}");
            
            if (Random.Shared.NextDouble() > 0.1)
            {
                var price = buttonColor == "red" ? 150.0 : 100.0;
                _client.Track("purchase", ref user, value: price);
                Console.WriteLine($"Tracked 'purchase' for button-color testing with value ${price}");
            }

            Console.WriteLine("\n[3] JSON Variation Evaluation");
            var uiConfig = _client.GetJsonVariation("ui-config", identity, context, new UiConfig());
            Console.WriteLine($"Flag 'ui-config' -> Theme: {uiConfig.Theme}, Sidebar: {uiConfig.ShowSidebar}, Items/Page: {uiConfig.ItemsPerPage}");

            Console.WriteLine("\n[4] Low-Level Guid Evaluation");
            var variationId = _client.Evaluate("backend-algo", ref user);
            Console.WriteLine($"Flag 'backend-algo' -> VariationId: {variationId}");

            Console.WriteLine("\n[5] Tracking Event with Value");
            if (isNewCheckoutEnabled)
            {
                var cartValue = 15.50 + Random.Shared.NextDouble() * 100;
                _client.Track("checkout_completed", identity, value: cartValue);
                Console.WriteLine($"Tracked 'checkout_completed' with value ${cartValue:F2}");
            }

            Console.WriteLine("\n[6] Tracking Event with Properties (Strongly Typed)");
            var purchaseProps = new PurchaseEventProperties 
            { 
                ItemId = "PROD-999", 
                Category = Random.Shared.NextDouble() > 0.5 ? "Electronics" : "Books", 
                IsSubscription = false 
            };
            var purchasePropsAcc = new ContextAccessor<PurchaseEventProperties>(purchaseProps);
            _client.Track("purchase", ref user, ref purchasePropsAcc, value: 299.99);
            Console.WriteLine("Tracked 'purchase' with structured properties.");

            await Task.Delay(5000, stoppingToken);
        }
    }
}