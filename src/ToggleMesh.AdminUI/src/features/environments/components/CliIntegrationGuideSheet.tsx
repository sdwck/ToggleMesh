import { Sheet, SheetContent, SheetDescription, SheetHeader, SheetTitle } from '@/components/ui/sheet';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Button } from '@/components/ui/button';
import { Copy } from 'lucide-react';
import { toast } from 'sonner';

interface CliIntegrationGuideSheetProps {
    open: boolean;
    onOpenChange: (open: boolean) => void;
}

export function CliIntegrationGuideSheet({ open, onOpenChange }: CliIntegrationGuideSheetProps) {
    const copyText = (text: string) => {
        navigator.clipboard.writeText(text);
        toast.success('Copied to clipboard');
    };

    return (
        <Sheet open={open} onOpenChange={onOpenChange}>
            <SheetContent className="w-full sm:max-w-xl overflow-y-auto bg-zinc-950 border-border/40">
                <SheetHeader>
                    <SheetTitle>CLI Setup Guide</SheetTitle>
                    <SheetDescription>Install and configure the ToggleMesh CLI for your environment.</SheetDescription>
                </SheetHeader>

                <Tabs defaultValue="cli-net" className="mt-6 space-y-4">
                    <TabsList className="bg-zinc-900 border border-border/10 h-10 overflow-x-auto flex flex-nowrap shrink-0 justify-start w-full px-2 items-center">
                        <TabsTrigger value="cli-net" className="text-xs shrink-0 whitespace-nowrap">.NET</TabsTrigger>
                        <TabsTrigger value="cli-js" className="text-xs shrink-0 whitespace-nowrap">Node/JS</TabsTrigger>
                        <TabsTrigger value="cli-python" className="text-xs shrink-0 whitespace-nowrap">Python</TabsTrigger>
                        <TabsTrigger value="cli-go" className="text-xs shrink-0 whitespace-nowrap">Go</TabsTrigger>
                    </TabsList>

                    <TabsContent value="cli-net" className="space-y-4">
                        <div className="space-y-1">
                            <h4 className="text-xs font-semibold text-muted-foreground uppercase">1. Install CLI Tool Globally</h4>
                            <div className="relative">
                                <pre className="bg-zinc-900/60 p-3 pr-10 overflow-x-auto whitespace-pre rounded-md font-mono text-xs border border-border/10 text-emerald-400">
                                    dotnet tool install -g ToggleMesh.CLI
                                </pre>
                                <Button variant="ghost" size="icon" onClick={() => copyText("dotnet tool install -g ToggleMesh.CLI")} className="absolute top-1.5 right-2 h-7 w-7 text-muted-foreground hover:text-foreground">
                                    <Copy className="h-3.5 w-3.5" />
                                </Button>
                            </div>
                        </div>

                        <div className="space-y-1">
                            <h4 className="text-xs font-semibold text-muted-foreground uppercase">2. Run Interactive Configuration</h4>
                            <div className="relative">
                                <pre className="bg-zinc-900/60 p-3 pr-10 overflow-x-auto whitespace-pre rounded-md font-mono text-xs border border-border/10 text-emerald-400">
                                    togglemesh config
                                </pre>
                                <Button variant="ghost" size="icon" onClick={() => copyText("togglemesh config")} className="absolute top-1.5 right-2 h-7 w-7 text-muted-foreground hover:text-foreground">
                                    <Copy className="h-3.5 w-3.5" />
                                </Button>
                            </div>
                            <p className="text-[10px] text-muted-foreground pl-1 mt-2">
                                Follow the prompts to configure your credentials and target project setup.
                            </p>
                        </div>

                        <div className="space-y-1">
                            <h4 className="text-xs font-semibold text-muted-foreground uppercase">3. Synchronize Feature Flags</h4>
                            <div className="relative">
                                <pre className="bg-zinc-900/60 p-3 pr-10 overflow-x-auto whitespace-pre rounded-md font-mono text-xs border border-border/10 text-emerald-400">
                                    togglemesh sync
                                </pre>
                                <Button variant="ghost" size="icon" onClick={() => copyText("togglemesh sync")} className="absolute top-1.5 right-2 h-7 w-7 text-muted-foreground hover:text-foreground">
                                    <Copy className="h-3.5 w-3.5" />
                                </Button>
                            </div>
                        </div>
                    </TabsContent>

                    <TabsContent value="cli-js" className="space-y-4">
                        <div className="space-y-1">
                            <h4 className="text-xs font-semibold text-muted-foreground uppercase">1. Install CLI as Dev Dependency (NPM)</h4>
                            <div className="relative">
                                <pre className="bg-zinc-900/60 p-3 pr-10 overflow-x-auto whitespace-pre rounded-md font-mono text-xs border border-border/10 text-emerald-400">
                                    npm install -D togglemesh
                                </pre>
                                <Button variant="ghost" size="icon" onClick={() => copyText("npm install -D togglemesh")} className="absolute top-1.5 right-2 h-7 w-7 text-muted-foreground hover:text-foreground">
                                    <Copy className="h-3.5 w-3.5" />
                                </Button>
                            </div>
                        </div>

                        <div className="space-y-1">
                            <h4 className="text-xs font-semibold text-muted-foreground uppercase">2. Run Interactive Configuration</h4>
                            <div className="relative">
                                <pre className="bg-zinc-900/60 p-3 pr-10 overflow-x-auto whitespace-pre rounded-md font-mono text-xs border border-border/10 text-emerald-400">
                                    npx togglemesh config
                                </pre>
                                <Button variant="ghost" size="icon" onClick={() => copyText("npx togglemesh config")} className="absolute top-1.5 right-2 h-7 w-7 text-muted-foreground hover:text-foreground">
                                    <Copy className="h-3.5 w-3.5" />
                                </Button>
                            </div>
                            <p className="text-[10px] text-muted-foreground pl-1 mt-2">
                                Follow the prompts to configure your credentials and target project setup.
                            </p>
                        </div>

                        <div className="space-y-1">
                            <h4 className="text-xs font-semibold text-muted-foreground uppercase">3. Synchronize Feature Flags</h4>
                            <div className="relative">
                                <pre className="bg-zinc-900/60 p-3 pr-10 overflow-x-auto whitespace-pre rounded-md font-mono text-xs border border-border/10 text-emerald-400">
                                    npx togglemesh sync
                                </pre>
                                <Button variant="ghost" size="icon" onClick={() => copyText("npx togglemesh sync")} className="absolute top-1.5 right-2 h-7 w-7 text-muted-foreground hover:text-foreground">
                                    <Copy className="h-3.5 w-3.5" />
                                </Button>
                            </div>
                        </div>
                    </TabsContent>

                    <TabsContent value="cli-python" className="space-y-4">
                        <div className="space-y-1">
                            <h4 className="text-xs font-semibold text-muted-foreground uppercase">1. Install Package</h4>
                            <div className="relative">
                                <pre className="bg-zinc-900/60 p-3 pr-10 overflow-x-auto whitespace-pre rounded-md font-mono text-xs border border-border/10 text-emerald-400">
                                    pip install togglemesh
                                </pre>
                                <Button variant="ghost" size="icon" onClick={() => copyText("pip install togglemesh")} className="absolute top-1.5 right-2 h-7 w-7 text-muted-foreground hover:text-foreground">
                                    <Copy className="h-3.5 w-3.5" />
                                </Button>
                            </div>
                        </div>
                        <div className="space-y-1">
                            <h4 className="text-xs font-semibold text-muted-foreground uppercase">2. Run Configuration</h4>
                            <div className="relative">
                                <pre className="bg-zinc-900/60 p-3 pr-10 overflow-x-auto whitespace-pre rounded-md font-mono text-xs border border-border/10 text-emerald-400">
                                    togglemesh config
                                </pre>
                                <Button variant="ghost" size="icon" onClick={() => copyText("togglemesh config")} className="absolute top-1.5 right-2 h-7 w-7 text-muted-foreground hover:text-foreground">
                                    <Copy className="h-3.5 w-3.5" />
                                </Button>
                            </div>
                            <p className="text-[10px] text-muted-foreground pl-1 mt-2">
                                Run this command in your project root to start the interactive configuration wizard.
                            </p>
                        </div>
                        <div className="space-y-1">
                            <h4 className="text-xs font-semibold text-muted-foreground uppercase">3. Synchronize</h4>
                            <div className="relative">
                                <pre className="bg-zinc-900/60 p-3 pr-10 overflow-x-auto whitespace-pre rounded-md font-mono text-xs border border-border/10 text-emerald-400">
                                    togglemesh sync
                                </pre>
                                <Button variant="ghost" size="icon" onClick={() => copyText("togglemesh sync")} className="absolute top-1.5 right-2 h-7 w-7 text-muted-foreground hover:text-foreground">
                                    <Copy className="h-3.5 w-3.5" />
                                </Button>
                            </div>
                        </div>
                    </TabsContent>

                    <TabsContent value="cli-go" className="space-y-4">
                        <div className="space-y-1">
                            <h4 className="text-xs font-semibold text-muted-foreground uppercase">1. Install CLI</h4>
                            <div className="relative">
                                <pre className="bg-zinc-900/60 p-3 pr-10 overflow-x-auto whitespace-pre rounded-md font-mono text-xs border border-border/10 text-emerald-400">
                                    go install github.com/sdwck/ToggleMesh/cmd/togglemesh@latest
                                </pre>
                                <Button variant="ghost" size="icon" onClick={() => copyText("go install github.com/sdwck/ToggleMesh/cmd/togglemesh@latest")} className="absolute top-1.5 right-2 h-7 w-7 text-muted-foreground hover:text-foreground">
                                    <Copy className="h-3.5 w-3.5" />
                                </Button>
                            </div>
                            <p className="text-[10px] text-muted-foreground pl-1 mt-2">
                                Ensure $GOPATH/bin is in your PATH.
                            </p>
                        </div>
                        <div className="space-y-1">
                            <h4 className="text-xs font-semibold text-muted-foreground uppercase">2. Setup and Sync</h4>
                            <div className="relative">
                                <pre className="bg-zinc-900/60 p-4 pr-10 overflow-x-auto whitespace-pre rounded-md font-mono text-xs border border-border/10 text-emerald-400">
                                    togglemesh config<br />togglemesh sync
                                </pre>
                                <Button variant="ghost" size="icon" onClick={() => copyText("togglemesh config\ntogglemesh sync")} className="absolute top-1.5 right-2 h-7 w-7 text-muted-foreground hover:text-foreground">
                                    <Copy className="h-3.5 w-3.5" />
                                </Button>
                            </div>
                        </div>
                    </TabsContent>
                </Tabs>
            </SheetContent>
        </Sheet>
    );
}
