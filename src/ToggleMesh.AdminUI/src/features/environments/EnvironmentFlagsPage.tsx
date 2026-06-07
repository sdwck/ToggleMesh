import { useState } from 'react';
import { useParams, useNavigate } from 'react-router-dom';
import { Plus, ArrowLeft } from 'lucide-react';
import { useFeatureFlags, useCreateFeatureFlag, useToggleFeatureFlag, useAuditLogs } from '@/api/queries';
import { Button } from '@/components/ui/button';
import { Card } from '@/components/ui/card';
import { Input } from '@/components/ui/input';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle, DialogTrigger } from '@/components/ui/dialog';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Skeleton } from '@/components/ui/skeleton';
import { Switch } from '@/components/ui/switch';
import { Badge } from '@/components/ui/badge';
import { toast } from 'sonner';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { Pagination, PaginationContent, PaginationItem, PaginationLink, PaginationNext, PaginationPrevious } from '@/components/ui/pagination';
import { FeatureFlagEditor } from '@/features/flags/FeatureFlagEditor';
import type { FeatureFlag, AuditLog } from '@/api/types';

const formatNumber = (num: number) => {
  if (num === 0) return '0';
  if (num >= 1000000) return (num / 1000000).toFixed(1).replace(/\.0$/, '') + 'm';
  if (num >= 1000) return (num / 1000).toFixed(1).replace(/\.0$/, '') + 'k';
  return num.toLocaleString();
};

const calculatePercent = (value: number, total: number) => {
  if (total === 0) return 0;
  return (value / total) * 100;
};

export function EnvironmentFlagsPage() {
  const { projectId, envId } = useParams<{ projectId: string; envId: string }>();
  const navigate = useNavigate();
  
  const [auditPage, setAuditPage] = useState(1);
  const { data: flags, isLoading: isLoadingFlags } = useFeatureFlags(projectId!, envId!);
  const { data: auditData, isLoading: isLoadingAudit } = useAuditLogs(envId!, auditPage);
  
  const createFlag = useCreateFeatureFlag(projectId!, envId!);
  const toggleFlag = useToggleFeatureFlag(projectId!, envId!);

  const [isCreateOpen, setIsCreateOpen] = useState(false);
  const [newFlagKey, setNewFlagKey] = useState('');

  const [selectedFlag, setSelectedFlag] = useState<FeatureFlag | null>(null);

  const [selectedAuditLog, setSelectedAuditLog] = useState<AuditLog | null>(null);

  const handleCreateFlag = async () => {
    if (!newFlagKey.trim()) return;
    try {
      await createFlag.mutateAsync(newFlagKey);
      toast.success('Feature flag created');
      setNewFlagKey('');
      setIsCreateOpen(false);
    } catch {
      toast.error('Failed to create flag');
    }
  };

  const handleToggle = async (e: React.MouseEvent, flagKey: string, currentVal: boolean) => {
    e.stopPropagation();
    try {
      await toggleFlag.mutateAsync({ flagKey, isEnabled: !currentVal });
      toast.success(`Flag ${!currentVal ? 'enabled' : 'disabled'}`);
    } catch {
      toast.error('Failed to toggle flag');
    }
  };

  return (
    <div className="space-y-6">
      <Button variant="ghost" size="sm" className="-ml-3 text-muted-foreground" onClick={() => navigate(`/projects/${projectId}`)}>
        <ArrowLeft className="mr-2 h-4 w-4" />
        Back to Environment List
      </Button>

      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-2xl font-bold tracking-tight">Feature Flags</h2>
          <p className="text-muted-foreground">Manage flags and targeting rules for this environment.</p>
        </div>
        <Dialog open={isCreateOpen} onOpenChange={setIsCreateOpen}>
          <DialogTrigger asChild>
            <Button>
              <Plus className="mr-2 h-4 w-4" />
              New Flag
            </Button>
          </DialogTrigger>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Create Feature Flag</DialogTitle>
              <DialogDescription>
                Use a descriptive, unique key (e.g., new-billing-ui).
              </DialogDescription>
            </DialogHeader>
            <div className="py-4">
              <Input
                placeholder="e.g., new-billing-ui"
                value={newFlagKey}
                onChange={(e) => setNewFlagKey(e.target.value)}
                autoFocus
                className="font-mono"
              />
            </div>
            <DialogFooter>
              <Button variant="outline" onClick={() => setIsCreateOpen(false)}>Cancel</Button>
              <Button onClick={handleCreateFlag} disabled={createFlag.isPending || !newFlagKey.trim()}>
                {createFlag.isPending ? 'Creating...' : 'Create'}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </div>

      <Tabs defaultValue="flags" className="w-full">
        <TabsList className="mb-4 bg-muted/50 border border-border/40">
          <TabsTrigger value="flags" className="data-[state=active]:bg-background">Flags</TabsTrigger>
          <TabsTrigger value="audit" className="data-[state=active]:bg-background">Audit Log</TabsTrigger>
        </TabsList>

        <TabsContent value="flags" className="m-0">
          <Card className="border-border/40 overflow-hidden">
            <Table>
              <TableHeader className="sticky top-0 bg-background z-10 h-[41px]">
                <TableRow className="hover:bg-transparent border-border/40 shadow-sm h-10">
                  <TableHead className="w-[300px]">Flag Key</TableHead>
                  <TableHead>Evaluations</TableHead>
                  <TableHead>Rules</TableHead>
                  <TableHead className="text-right">Status</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoadingFlags ? (
                  Array.from({ length: 3 }).map((_, i) => (
                    <TableRow key={i} className="border-border/40">
                      <TableCell><Skeleton className="h-4 w-[200px]" /></TableCell>
                      <TableCell><Skeleton className="h-4 w-[60px]" /></TableCell>
                      <TableCell><Skeleton className="h-4 w-[60px]" /></TableCell>
                      <TableCell><Skeleton className="h-4 w-[100px]" /></TableCell>
                      <TableCell className="text-right"><Skeleton className="h-6 w-12 ml-auto" /></TableCell>
                    </TableRow>
                  ))
                ) : flags?.length === 0 ? (
                  <TableRow className="border-border/40">
                    <TableCell colSpan={5} className="h-24 text-center text-muted-foreground">
                      No feature flags found. Create one to get started.
                    </TableCell>
                  </TableRow>
                ) : (
                  flags?.map((flag) => (
                    <TableRow 
                      key={flag.id} 
                      className="border-border/40 hover:bg-muted/30 cursor-pointer"
                      onClick={() => setSelectedFlag(flag)}
                    >
                      <TableCell className="font-medium font-mono text-sm">
                        {flag.key}
                      </TableCell>
                      <TableCell>
                        <div className="flex flex-col gap-1.5 w-[140px] pr-4">
                          <div className="flex items-center justify-between font-mono text-[13px] leading-none">
                            <span className={flag.trueCount > 0 ? "text-emerald-500/90 font-medium" : "text-muted-foreground/40"}>
                              <span className="text-[10px] text-muted-foreground/50 mr-1">T</span>
                              {formatNumber(flag.trueCount)}
                            </span>
                            <span className={flag.falseCount > 0 ? "text-rose-500/90 font-medium" : "text-muted-foreground/40"}>
                              {formatNumber(flag.falseCount)}
                              <span className="text-[10px] text-muted-foreground/50 ml-1">F</span>
                            </span>
                          </div>
                          <div className="h-1 w-full bg-secondary/40 rounded-full overflow-hidden flex">
                            {(flag.trueCount > 0 || flag.falseCount > 0) ? (
                              <>
                                <div 
                                  className="h-full bg-emerald-500/60 transition-all" 
                                  style={{ width: `${calculatePercent(flag.trueCount, flag.trueCount + flag.falseCount)}%` }} 
                                />
                                <div 
                                  className="h-full bg-rose-500/60 transition-all" 
                                  style={{ width: `${calculatePercent(flag.falseCount, flag.trueCount + flag.falseCount)}%` }} 
                                />
                              </>
                            ) : (
                              <div className="h-full w-full bg-muted-foreground/10" />
                            )}
                          </div>
                        </div>
                      </TableCell>
                      <TableCell>
                        <div className="flex gap-2">
                          {flag.rules?.length > 0 && (
                            <Badge variant="secondary" className="bg-primary/10 text-primary min-w-[72px] justify-center">
                              {flag.rules.length} {flag.rules.length === 1 ? 'Rule' : 'Rules'}
                            </Badge>
                          )}
                          {flag.rolloutPercentage !== null && (
                            <Badge variant="secondary" className="bg-accent text-accent-foreground">
                              {flag.rolloutPercentage}% Rollout
                            </Badge>
                          )}
                          {(!flag.rules || flag.rules.length === 0) && flag.rolloutPercentage === null && (
                            <span className="text-xs text-muted-foreground">Default</span>
                          )}
                        </div>
                      </TableCell>
                      <TableCell className="text-right">
                        <div className="flex items-center justify-end gap-3" onClick={(e) => e.stopPropagation()}>
                          <span className={`text-sm font-medium ${flag.isEnabled ? 'text-primary' : 'text-muted-foreground'}`}>
                            {flag.isEnabled ? 'ON' : 'OFF'}
                          </span>
                          <Switch
                            checked={flag.isEnabled}
                            onCheckedChange={() => handleToggle({ stopPropagation: () => {} } as React.MouseEvent, flag.key, flag.isEnabled)}
                            disabled={toggleFlag.isPending}
                          />
                        </div>
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </Card>
        </TabsContent>

        <TabsContent value="audit" className="m-0 space-y-4">
          <Card className="border-border/40 overflow-hidden">
            <Table wrapperClassName="max-h-[571px] snap-y snap-mandatory scroll-pt-[41px]">
              <TableHeader className="sticky top-0 bg-background z-10 h-[41px]">
                <TableRow className="hover:bg-transparent border-border/40 shadow-sm h-10">
                  <TableHead className="w-[180px]">Timestamp</TableHead>
                  <TableHead className="w-[120px]">Action</TableHead>
                  <TableHead>Entity</TableHead>
                  <TableHead>Performed By</TableHead>
                  <TableHead className="text-right">Details</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {isLoadingAudit ? (
                  <TableRow className="border-border/40 h-[53px] snap-start"><TableCell colSpan={5}><Skeleton className="h-4 w-full" /></TableCell></TableRow>
                ) : auditData?.items.length === 0 ? (
                  <TableRow className="border-border/40 h-[53px] snap-start">
                    <TableCell colSpan={5} className="h-24 text-center text-muted-foreground">
                      No audit logs for this environment yet.
                    </TableCell>
                  </TableRow>
                ) : (
                  auditData?.items.map((log) => (
                    <TableRow key={log.id} className="border-border/40 hover:bg-muted/30 text-sm h-[53px] snap-start">
                      <TableCell className="text-muted-foreground py-2">
                        {new Date(log.timestamp).toLocaleString()}
                      </TableCell>
                      <TableCell className="py-2">
                        <Badge variant="outline" className="text-xs font-mono">
                          {log.action}
                        </Badge>
                      </TableCell>
                      <TableCell className="font-mono text-xs py-2">{log.entityName}</TableCell>
                      <TableCell className="text-muted-foreground py-2">{log.performedBy}</TableCell>
                      <TableCell className="text-right py-2">
                        <Button 
                          variant="ghost" 
                          size="sm" 
                          onClick={() => setSelectedAuditLog(log)}
                          disabled={!log.oldValues && !log.newValues}
                          className="h-8"
                        >
                          View
                        </Button>
                      </TableCell>
                    </TableRow>
                  ))
                )}
              </TableBody>
            </Table>
          </Card>
          
          {auditData && auditData.totalPages > 1 && (
            <Pagination>
              <PaginationContent>
                <PaginationItem>
                  <PaginationPrevious 
                    onClick={() => setAuditPage(p => Math.max(1, p - 1))}
                    className={!auditData.hasPreviousPage ? "pointer-events-none opacity-50" : "cursor-pointer"}
                  />
                </PaginationItem>
                
                {Array.from({ length: auditData.totalPages }).map((_, i) => {
                  const pageNumber = i + 1;
                  if (
                    pageNumber === 1 ||
                    pageNumber === auditData.totalPages ||
                    (pageNumber >= auditPage - 1 && pageNumber <= auditPage + 1)
                  ) {
                    return (
                      <PaginationItem key={pageNumber}>
                        <PaginationLink 
                          isActive={pageNumber === auditPage}
                          onClick={() => setAuditPage(pageNumber)}
                          className="cursor-pointer"
                        >
                          {pageNumber}
                        </PaginationLink>
                      </PaginationItem>
                    );
                  }
                  if (pageNumber === auditPage - 2 || pageNumber === auditPage + 2) {
                    return <span key={pageNumber} className="px-2 text-muted-foreground">...</span>;
                  }
                  return null;
                })}

                <PaginationItem>
                  <PaginationNext 
                    onClick={() => setAuditPage(p => Math.min(auditData.totalPages, p + 1))}
                    className={!auditData.hasNextPage ? "pointer-events-none opacity-50" : "cursor-pointer"}
                  />
                </PaginationItem>
              </PaginationContent>
            </Pagination>
          )}
        </TabsContent>
      </Tabs>

      <FeatureFlagEditor
        flag={selectedFlag}
        projectId={projectId!}
        envId={envId!}
        open={!!selectedFlag}
        onOpenChange={(open) => !open && setSelectedFlag(null)}
      />

      <Dialog open={!!selectedAuditLog} onOpenChange={(open) => !open && setSelectedAuditLog(null)}>
        <DialogContent className="max-w-3xl max-h-[80vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Audit Log Details</DialogTitle>
            <DialogDescription>
              {selectedAuditLog?.action} on {selectedAuditLog?.entityName}
            </DialogDescription>
          </DialogHeader>
          
          <div className="grid grid-cols-2 gap-4 mt-4">
            <div className="space-y-2">
              <h4 className="text-sm font-medium text-muted-foreground">Previous State</h4>
              <div className="bg-muted p-4 rounded-md font-mono text-xs overflow-x-auto whitespace-pre-wrap">
                {selectedAuditLog?.oldValues ? (
                  <pre>{JSON.stringify(JSON.parse(selectedAuditLog.oldValues), null, 2)}</pre>
                ) : (
                  <span className="text-muted-foreground">None</span>
                )}
              </div>
            </div>
            <div className="space-y-2">
              <h4 className="text-sm font-medium text-muted-foreground">New State</h4>
              <div className="bg-muted p-4 rounded-md font-mono text-xs overflow-x-auto whitespace-pre-wrap">
                {selectedAuditLog?.newValues ? (
                  <pre>{JSON.stringify(JSON.parse(selectedAuditLog.newValues), null, 2)}</pre>
                ) : (
                  <span className="text-muted-foreground">None</span>
                )}
              </div>
            </div>
          </div>
          
          <DialogFooter>
            <Button onClick={() => setSelectedAuditLog(null)}>Close</Button>
          </DialogFooter>
        </DialogContent>
      </Dialog>
    </div>
  );
}
