import { Card, CardContent, CardDescription, CardHeader, CardTitle, CardFooter } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Accordion, AccordionContent, AccordionItem, AccordionTrigger } from '@/components/ui/accordion';
import { MessageSquare, Mail, LifeBuoy, Zap, ArrowLeft } from 'lucide-react';
import { Link } from 'react-router-dom';
import { ToggleMeshIcon } from '@/components/icons/ToggleMeshIcon';

export function SupportPage() {
    return (
        <div className="min-h-screen flex flex-col bg-background text-foreground">
            <header className="h-14 border-b border-border/40 bg-zinc-950/80 backdrop-blur flex items-center justify-between px-6 z-10 shrink-0">
                <div className="flex items-center gap-2">
                    <Link to="/" className="flex items-center text-zinc-400 hover:text-white transition-colors mr-2 group">
                        <ToggleMeshIcon className="h-5 w-5 mr-2 transition-colors duration-300 group-hover:text-white" />
                        <span className="font-semibold tracking-tight">ToggleMesh</span>
                    </Link>
                </div>
                <div className="flex items-center">
                    <Button variant="ghost" size="sm" asChild className="text-zinc-400 hover:text-white">
                        <Link to="/">
                            <ArrowLeft className="mr-2 h-4 w-4" /> Back to App
                        </Link>
                    </Button>
                </div>
            </header>

            <main className="flex-1 overflow-auto">
                <div className="max-w-4xl mx-auto space-y-8 pb-10 pt-8 px-6">
                    <div>
                        <h1 className="text-3xl font-bold tracking-tight">Support & Resources</h1>
                        <p className="text-muted-foreground mt-2 text-sm">
                            Find help, join the community, or get priority support for your Enterprise needs.
                        </p>
                    </div>

            <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                <Card className="border-border/40 bg-zinc-950/20 backdrop-blur-sm flex flex-col">
                    <CardHeader>
                        <div className="h-10 w-10 rounded-lg bg-zinc-900 border border-zinc-800 flex items-center justify-center mb-2">
                            <MessageSquare className="h-5 w-5 text-zinc-300" />
                        </div>
                        <CardTitle className="text-lg">Community Support</CardTitle>
                        <CardDescription>
                            Free support for Open Source users. Report bugs, request features, or ask questions in our community.
                        </CardDescription>
                    </CardHeader>
                    <CardContent className="flex-1">
                        <ul className="text-sm space-y-2 text-zinc-400">
                            <li>• Browse existing issues and discussions</li>
                            <li>• Report bugs with detailed reproduction steps</li>
                            <li>• Propose new features and API improvements</li>
                        </ul>
                    </CardContent>
                    <CardFooter className="pt-4 border-t border-border/10 mt-auto">
                        <Button variant="outline" className="w-full" asChild>
                            <a href="https://github.com/sdwck/ToggleMesh/issues" target="_blank" rel="noopener noreferrer">
                                <MessageSquare className="mr-2 h-4 w-4" /> Open an Issue
                            </a>
                        </Button>
                    </CardFooter>
                </Card>

                <Card className="border-primary/50 bg-primary/5 backdrop-blur-sm flex flex-col relative overflow-hidden">
                    <div className="absolute top-0 right-0 w-32 h-32 bg-primary/10 rounded-full blur-3xl -mr-10 -mt-10 pointer-events-none" />
                    <CardHeader>
                        <div className="flex items-center justify-between mb-2">
                            <div className="h-10 w-10 rounded-lg bg-primary/20 border border-primary/30 flex items-center justify-center">
                                <LifeBuoy className="h-5 w-5 text-primary" />
                            </div>
                            <span className="flex items-center gap-1 text-[10px] font-semibold tracking-wider uppercase text-primary bg-primary/10 px-2 py-0.5 rounded-full border border-primary/20">
                                <Zap className="h-3 w-3" /> Priority
                            </span>
                        </div>
                        <CardTitle className="text-lg">Enterprise Support</CardTitle>
                        <CardDescription>
                            Guaranteed SLAs, direct developer access, custom integrations, and priority bug fixes.
                        </CardDescription>
                    </CardHeader>
                    <CardContent className="flex-1">
                        <ul className="text-sm space-y-2 text-zinc-400">
                            <li>• 1-hour response time SLA</li>
                            <li>• Architecture & deployment reviews</li>
                            <li>• Custom SSO & RBAC integrations</li>
                        </ul>
                    </CardContent>
                    <CardFooter className="pt-4 border-t border-primary/20 mt-auto">
                        <Button className="w-full" asChild>
                            <a href="mailto:sdwcktarakanov@gmail.com">
                                <Mail className="mr-2 h-4 w-4" /> Contact Sales
                            </a>
                        </Button>
                    </CardFooter>
                </Card>
            </div>

            <div className="mt-12 space-y-6">
                <div>
                    <h2 className="text-xl font-semibold tracking-tight">Frequently Asked Questions</h2>
                    <p className="text-sm text-muted-foreground mt-1">Quick answers to common questions about ToggleMesh.</p>
                </div>

                <Card className="border-border/40 bg-zinc-950/20 backdrop-blur-sm">
                    <CardContent className="p-0">
                        <Accordion type="single" collapsible className="w-full px-6 py-2">
                            <AccordionItem value="item-1" className="border-b-border/20">
                                <AccordionTrigger className="text-sm hover:no-underline hover:text-primary transition-colors">
                                    Where is my data stored?
                                </AccordionTrigger>
                                <AccordionContent className="text-zinc-400 text-sm leading-relaxed">
                                    ToggleMesh is self-hosted. All feature flag data, user metrics, and evaluation rules reside entirely within your own PostgreSQL/Redis infrastructure. We track nothing.
                                </AccordionContent>
                            </AccordionItem>

                            <AccordionItem value="item-2" className="border-b-border/20">
                                <AccordionTrigger className="text-sm hover:no-underline hover:text-primary transition-colors">
                                    How do I rotate a compromised API key?
                                </AccordionTrigger>
                                <AccordionContent className="text-zinc-400 text-sm leading-relaxed">
                                    Go to <strong>Project &rarr; Environments</strong>, locate the compromised environment, and click "Rotate Key". Update your SDK immediately as the old key is instantly revoked and cannot be recovered.
                                </AccordionContent>
                            </AccordionItem>

                            <AccordionItem value="item-3" className="border-b-border/20">
                                <AccordionTrigger className="text-sm hover:no-underline hover:text-primary transition-colors">
                                    Why are my metrics not appearing?
                                </AccordionTrigger>
                                <AccordionContent className="text-zinc-400 text-sm leading-relaxed">
                                    Ensure that the SDK background flusher is running and your <code>AnalyticsBufferSize</code> is sufficient. Metrics are flushed in batches every 10 seconds. Also verify that you are connecting to the correct Environment API Key.
                                </AccordionContent>
                            </AccordionItem>

                            <AccordionItem value="item-4" className="border-none border-b-0">
                                <AccordionTrigger className="text-sm hover:no-underline hover:text-primary transition-colors">
                                    Do you offer managed hosting?
                                </AccordionTrigger>
                                <AccordionContent className="text-zinc-400 text-sm leading-relaxed">
                                    Currently, ToggleMesh is a self-hosted-first platform designed for maximum privacy and control. For fully managed Enterprise deployments and custom cloud solutions, please contact our Sales team.
                                </AccordionContent>
                            </AccordionItem>
                        </Accordion>
                    </CardContent>
                </Card>
            </div>
        </div>
    </main>
</div>
    );
}
