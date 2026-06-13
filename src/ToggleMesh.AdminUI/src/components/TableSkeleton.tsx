import { Skeleton } from "@/components/ui/skeleton";
import { TableRow, TableCell } from "@/components/ui/table";
import { MoreHorizontal } from "lucide-react";
import { Switch } from "@/components/ui/switch";

interface TableSkeletonProps {
    columnsCount: number;
    rowsCount?: number;
}

export function TableSkeleton({ columnsCount, rowsCount = 5 }: TableSkeletonProps) {
    return (
        <>
            {Array.from({ length: rowsCount }).map((_, rowIndex) => (
                <TableRow key={rowIndex} className="h-[53px] border-border/40 hover:bg-transparent">
                    {Array.from({ length: columnsCount }).map((_, colIndex) => {
                        const isFirst = colIndex === 0;
                        const isLast = colIndex === columnsCount - 1;

                        return (
                            <TableCell
                                key={colIndex}
                                className={
                                    isFirst
                                        ? "sticky left-0 bg-zinc-950 z-10"
                                        : isLast
                                            ? "sticky right-0 bg-zinc-950 z-10 border-l border-border/10"
                                            : ""
                                }
                            >
                                {isFirst && (
                                    <Skeleton className="h-5 w-[180px] rounded-md" />
                                )}

                                {isLast && (
                                    <div className="flex justify-end pr-4 animate-pulse">
                                        <MoreHorizontal className="h-4 w-4 text-zinc-700" />
                                    </div>
                                )}

                                {!isFirst && !isLast && (
                                    <div className="flex justify-center items-center h-full animate-pulse opacity-45">
                                        <Switch checked={false} disabled />
                                    </div>
                                )}
                            </TableCell>
                        );
                    })}
                </TableRow>
            ))}
        </>
    );
}