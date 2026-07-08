import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from '@/components/ui/sheet';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Button } from '@/components/ui/button';
import { Copy } from 'lucide-react';
import { toast } from 'sonner';

interface SdkIntegrationGuideSheetProps {
    open: boolean;
    onOpenChange: (open: boolean) => void;
    serverKey: string;
    clientKey: string;
}

export function SdkIntegrationGuideSheet({ open, onOpenChange, serverKey, clientKey }: SdkIntegrationGuideSheetProps) {
    const copyText = (text: string) => {
        navigator.clipboard.writeText(text);
        toast.success('Copied to clipboard');
    };

    const csharpRegisterCode = `// Register ToggleMesh SDK
builder.Services.AddToggleMeshClient(options => {
    options.BaseUrl = "${window.location.origin}";
    options.ApiKey = "${serverKey}";
});

// Optional: Enable automatic context mapping from HttpContext
builder.Services.AddToggleMeshHttpContext();`;

    const csharpUsageCode = `public class CheckoutService {
    private readonly IToggleMeshClient _toggleMesh;

    public CheckoutService(IToggleMeshClient toggleMesh) {
        _toggleMesh = toggleMesh;
    }

    public void ProcessPayment() {
        // Evaluated in ~37ns. No HTTP request made.
        if (_toggleMesh.IsEnabled("new-checkout-flow")) {
            // New PayPal integration
        }
    }
}`;

    const typescriptRegisterCode = `import { ToggleMeshClient } from 'togglemesh-js';
import { ToggleMeshProvider } from 'togglemesh-js/react';

const client = new ToggleMeshClient({
    baseUrl: "${window.location.origin}",
    clientKey: "${clientKey}",
    refreshInterval: 30
});

// Identify the user (optional but recommended for targeting)
await client.identify("user_123", { 
    Country: "US", 
    Plan: "Pro" 
});

// Wrap your app with the provider
ReactDOM.createRoot(document.getElementById('root')!).render(
  <ToggleMeshProvider client={client}>
    <App />
  </ToggleMeshProvider>
);`;

    const typescriptUsageCode = `import { useFeatureFlag } from 'togglemesh-js/react';

export function PaymentComponent() {
    const showPaypal = useFeatureFlag('new-checkout-flow');
    // Render component conditionally based on flag
    return showPaypal ? <PayPalButton /> : <CreditCardButton />;
}`;

    const nodeRegisterCode = `import { ToggleMeshClient } from 'togglemesh-node';

// Initialize the client with a server key
const client = new ToggleMeshClient({
    baseUrl: "${window.location.origin}",
    serverKey: "${serverKey}"
});

// Start the client to synchronize initial state
await client.start();
const context = { tenant: "acme_corp" };

// Evaluate the flag for a specific user
const isEnabled = client.isEnabled("new-feature", { identity: "user_123", context });`;

    const pythonRegisterCode = `from togglemesh import ToggleMeshClient, ToggleMeshOptions

# Initialize the client
client = ToggleMeshClient(ToggleMeshOptions(
    base_url="${window.location.origin}",
    client_key="${serverKey}"
))

# Evaluate the flag
context = {"tenant": "acme_corp"}
if client.is_enabled("new-feature", identity="user-123", context=context, default_value=False):
    print("Feature is ON")`;

    const goRegisterCode = `import "github.com/sdwck/ToggleMesh/sdks/go/togglemesh"

// Initialize the client
options := &togglemesh.ToggleMeshOptions{
    BaseURL: "${window.location.origin}",
    APIKey:  "${serverKey}", 
}
client, err := togglemesh.NewClient(options)

// Evaluate the flag
context := map[string]any{"Email": "test@gmail.com"}
enabled := client.IsEnabled("new-feature", false, "user_123", context)`;

    const unrealRegisterCode = `1. Edit > Project Settings > Plugins > ToggleMesh
2. Set Base Url to: ${window.location.origin}
3. Set Client Key to: ${clientKey}`;

    return (
        <Sheet open={open} onOpenChange={onOpenChange}>
            <SheetContent className="w-full sm:max-w-xl overflow-y-auto bg-zinc-950 border-border/40">
                <SheetHeader>
                    <SheetTitle>SDK Integration Guide</SheetTitle>
                    <SheetDescription>Copy and paste these snippets to integrate ToggleMesh into your app.</SheetDescription>
                </SheetHeader>

                <Tabs defaultValue="csharp" className="mt-6 space-y-4">
                    <TabsList className="bg-zinc-900 border border-border/10 h-10 overflow-x-auto flex flex-nowrap shrink-0 justify-start w-full px-2 items-center">
                        <TabsTrigger value="csharp" className="text-xs shrink-0 whitespace-nowrap">.NET</TabsTrigger>
                        <TabsTrigger value="typescript" className="text-xs shrink-0 whitespace-nowrap">React/TS (Client)</TabsTrigger>
                        <TabsTrigger value="node" className="text-xs shrink-0 whitespace-nowrap">Node.js</TabsTrigger>
                        <TabsTrigger value="python" className="text-xs shrink-0 whitespace-nowrap">Python</TabsTrigger>
                        <TabsTrigger value="go" className="text-xs shrink-0 whitespace-nowrap">Go</TabsTrigger>
                        <TabsTrigger value="unreal" className="text-xs shrink-0 whitespace-nowrap">Unreal Engine</TabsTrigger>
                    </TabsList>

                    <TabsContent value="csharp" className="space-y-4">
                        <div className="space-y-1">
                            <h4 className="text-xs font-semibold text-muted-foreground uppercase">1. Install Package</h4>
                            <pre className="bg-zinc-900/60 p-3 rounded-md font-mono text-xs border border-border/10 text-emerald-400">
                                dotnet add package ToggleMesh.SDK
                            </pre>
                        </div>
                        <div className="space-y-1">
                            <h4 className="text-xs font-semibold text-muted-foreground uppercase">2. Register Client</h4>
                            <div className="relative">
                                <pre className="bg-zinc-900/60 p-4 rounded-md font-mono text-xs overflow-auto border border-border/10 text-emerald-400">
                                    {csharpRegisterCode}
                                </pre>
                                <Button variant="ghost" size="icon" onClick={() => copyText(csharpRegisterCode)} className="absolute top-2 right-2 h-7 w-7 text-muted-foreground hover:text-foreground">
                                    <Copy className="h-3.5 w-3.5" />
                                </Button>
                            </div>
                            <div className="space-y-1 mt-4">
                                <h4 className="text-xs font-semibold text-muted-foreground uppercase">3. Use Client</h4>
                                <div className="relative">
                                    <pre className="bg-zinc-900/60 p-4 rounded-md font-mono text-xs overflow-auto border border-border/10 text-emerald-400">
                                        {csharpUsageCode}
                                    </pre>
                                    <Button variant="ghost" size="icon" onClick={() => copyText(csharpUsageCode)} className="absolute top-2 right-2 h-7 w-7 text-muted-foreground hover:text-foreground">
                                        <Copy className="h-3.5 w-3.5" />
                                    </Button>
                                </div>
                            </div>
                        </div>
                    </TabsContent>

                    <TabsContent value="typescript" className="space-y-4">
                        <div className="space-y-1">
                            <h4 className="text-xs font-semibold text-muted-foreground uppercase">1. Install Package</h4>
                            <pre className="bg-zinc-900/60 p-3 rounded-md font-mono text-xs border border-border/10 text-emerald-400">
                                npm install togglemesh-js
                            </pre>
                        </div>
                        <div className="space-y-1">
                            <h4 className="text-xs font-semibold text-muted-foreground uppercase">2. Initialize Client</h4>
                            <div className="relative">
                                <pre className="bg-zinc-900/60 p-4 rounded-md font-mono text-xs overflow-auto border border-border/10 text-emerald-400">
                                    {typescriptRegisterCode}
                                </pre>
                                <Button variant="ghost" size="icon" onClick={() => copyText(typescriptRegisterCode)} className="absolute top-2 right-2 h-7 w-7 text-muted-foreground hover:text-foreground">
                                    <Copy className="h-3.5 w-3.5" />
                                </Button>
                            </div>
                        </div>
                        <div className="space-y-1 mt-4">
                            <h4 className="text-xs font-semibold text-muted-foreground uppercase">3. Use Client</h4>
                            <div className="relative">
                                <pre className="bg-zinc-900/60 p-4 rounded-md font-mono text-xs overflow-auto border border-border/10 text-emerald-400">
                                    {typescriptUsageCode}
                                </pre>
                                <Button variant="ghost" size="icon" onClick={() => copyText(typescriptUsageCode)} className="absolute top-2 right-2 h-7 w-7 text-muted-foreground hover:text-foreground">
                                    <Copy className="h-3.5 w-3.5" />
                                </Button>
                            </div>
                        </div>
                    </TabsContent>

                    <TabsContent value="node" className="space-y-4">
                        <div className="space-y-1">
                            <h4 className="text-xs font-semibold text-muted-foreground uppercase">1. Install Package</h4>
                            <pre className="bg-zinc-900/60 p-3 rounded-md font-mono text-xs border border-border/10 text-emerald-400">
                                npm install togglemesh-node
                            </pre>
                        </div>
                        <div className="space-y-1">
                            <h4 className="text-xs font-semibold text-muted-foreground uppercase">2. Use Client</h4>
                            <div className="relative">
                                <pre className="bg-zinc-900/60 p-4 rounded-md font-mono text-xs overflow-auto border border-border/10 text-emerald-400">
                                    {nodeRegisterCode}
                                </pre>
                                <Button variant="ghost" size="icon" onClick={() => copyText(nodeRegisterCode)} className="absolute top-2 right-2 h-7 w-7 text-muted-foreground hover:text-foreground">
                                    <Copy className="h-3.5 w-3.5" />
                                </Button>
                            </div>
                        </div>
                    </TabsContent>

                    <TabsContent value="python" className="space-y-4">
                        <div className="space-y-1">
                            <h4 className="text-xs font-semibold text-muted-foreground uppercase">1. Install Package</h4>
                            <pre className="bg-zinc-900/60 p-3 rounded-md font-mono text-xs border border-border/10 text-emerald-400">
                                pip install togglemesh
                            </pre>
                        </div>
                        <div className="space-y-1">
                            <h4 className="text-xs font-semibold text-muted-foreground uppercase">2. Use Client</h4>
                            <div className="relative">
                                <pre className="bg-zinc-900/60 p-4 rounded-md font-mono text-xs overflow-auto border border-border/10 text-emerald-400">
                                    {pythonRegisterCode}
                                </pre>
                                <Button variant="ghost" size="icon" onClick={() => copyText(pythonRegisterCode)} className="absolute top-2 right-2 h-7 w-7 text-muted-foreground hover:text-foreground">
                                    <Copy className="h-3.5 w-3.5" />
                                </Button>
                            </div>
                        </div>
                    </TabsContent>

                    <TabsContent value="go" className="space-y-4">
                        <div className="space-y-1">
                            <h4 className="text-xs font-semibold text-muted-foreground uppercase">1. Install Package</h4>
                            <pre className="bg-zinc-900/60 p-3 rounded-md font-mono text-xs border border-border/10 text-emerald-400">
                                go get github.com/sdwck/ToggleMesh/sdks/go@latest
                            </pre>
                        </div>
                        <div className="space-y-1">
                            <h4 className="text-xs font-semibold text-muted-foreground uppercase">2. Use Client</h4>
                            <div className="relative">
                                <pre className="bg-zinc-900/60 p-4 rounded-md font-mono text-xs overflow-auto border border-border/10 text-emerald-400">
                                    {goRegisterCode}
                                </pre>
                                <Button variant="ghost" size="icon" onClick={() => copyText(goRegisterCode)} className="absolute top-2 right-2 h-7 w-7 text-muted-foreground hover:text-foreground">
                                    <Copy className="h-3.5 w-3.5" />
                                </Button>
                            </div>
                        </div>
                    </TabsContent>

                    <TabsContent value="unreal" className="space-y-4">
                        <div className="space-y-1">
                            <h4 className="text-xs font-semibold text-muted-foreground uppercase">Configuration</h4>
                            <div className="relative">
                                <pre className="bg-zinc-900/60 p-4 rounded-md font-mono text-xs overflow-auto border border-border/10 text-emerald-400">
                                    {unrealRegisterCode}
                                </pre>
                                <Button variant="ghost" size="icon" onClick={() => copyText(unrealRegisterCode)} className="absolute top-2 right-2 h-7 w-7 text-muted-foreground hover:text-foreground">
                                    <Copy className="h-3.5 w-3.5" />
                                </Button>
                            </div>
                            <p className="text-[10px] text-muted-foreground pl-1 mt-2">
                                Download the plugin source and copy into the Plugins/ folder of your project. Check the GitHub repository for full instructions.
                            </p>
                        </div>
                    </TabsContent>
                </Tabs>
            </SheetContent>
        </Sheet>
    );
}
