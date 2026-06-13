import { useState } from 'react';
import { useProjectAuditLogs } from '@/api/queries';
import { Card } from '@/components/ui/card';
import { Table, TableBody, TableCell, TableHead, TableHeader, TableRow } from '@/components/ui/table';
import { Skeleton } from '@/components/ui/skeleton';
import { Badge } from '@/components/ui/badge';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Button } from '@/components/ui/button';
import type { ProjectDetails, AuditLog } from '@/api/types';
import { PaginationControls } from '@/components/ui/PaginationControls';

const formatDate = (dateString: string) => {
  const date = new Date(dateString);
  const pad = (num: number) => String(num).padStart(2, '0');
  
  const year = date.getFullYear();
  const month = pad(date.getMonth() + 1);
  const day = pad(date.getDate());
  const hours = pad(date.getHours());
  const minutes = pad(date.getMinutes());
  const seconds = pad(date.getSeconds());
  
  return `${year}-${month}-${day} ${hours}:${minutes}:${seconds}`;
};

export function ProjectAuditTab({ project }: { project: ProjectDetails }) {
  const [auditPage, setAuditPage] = useState(1);
  const { data: auditData, isLoading: isLoadingAudit } = useProjectAuditLogs(project.id, auditPage, 10);
  
  const [selectedAuditLog, setSelectedAuditLog] = useState<AuditLog | null>(null);

  return (
    <div className="space-y-4">
      <Card className="border-border/40 bg-zinc-950/20 overflow-hidden">
        <div className="overflow-x-auto">
          <Table>
          <TableHeader>
            <TableRow className="hover:bg-transparent border-border/40 shadow-sm">
              <TableHead className="w-[180px]">Timestamp</TableHead>
              <TableHead className="w-[120px]">Action</TableHead>
              <TableHead>Entity</TableHead>
              <TableHead>Friendly Name</TableHead>
              <TableHead>Performed By</TableHead>
              <TableHead className="text-right">Details</TableHead>
            </TableRow>
          </TableHeader>
          <TableBody>
            {isLoadingAudit ? (
              <TableRow className="border-border/40 h-[53px]">
                <TableCell colSpan={6}><Skeleton className="h-4 w-full" /></TableCell>
              </TableRow>
            ) : auditData?.items.length === 0 ? (
              <TableRow className="border-border/40 h-[53px]">
                <TableCell colSpan={6} className="h-24 text-center text-muted-foreground">
                  No audit logs for this project yet.
                </TableCell>
              </TableRow>
            ) : (
              auditData?.items.map((log) => (
                <TableRow key={log.id} className="border-border/40 hover:bg-muted/30 text-sm h-[53px]">
                  <TableCell className="text-muted-foreground py-2 font-mono text-xs">
                    {formatDate(log.timestamp)}
                  </TableCell>
                  <TableCell className="py-2">
                    <Badge variant="outline" className="text-xs font-mono">
                      {log.action}
                    </Badge>
                  </TableCell>
                  <TableCell className="font-mono text-xs py-2">{log.entityName}</TableCell>
                  <TableCell className="font-medium text-xs py-2">{log.entityFriendlyName || log.entityId}</TableCell>
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
        </div>
      </Card>
      
      {auditData && auditData.totalPages > 1 && (
        <div className="shrink-0 pt-2">
          <PaginationControls 
            currentPage={auditPage}
            totalPages={auditData.totalPages}
            onPageChange={setAuditPage}
            hasNextPage={auditData.hasNextPage}
            hasPreviousPage={auditData.hasPreviousPage}
          />
        </div>
      )}

      <Dialog open={!!selectedAuditLog} onOpenChange={(open) => !open && setSelectedAuditLog(null)}>
        <DialogContent className="max-w-3xl max-h-[80vh] overflow-y-auto border-border/40 bg-zinc-950">
          <DialogHeader>
            <DialogTitle>Audit Log Details</DialogTitle>
            <DialogDescription>
              {selectedAuditLog?.action} on {selectedAuditLog?.entityName} ({selectedAuditLog?.entityFriendlyName || selectedAuditLog?.entityId})
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
