import { useState, useEffect } from "react";
import { Label } from "@/components/ui/label";

import { Slider } from "@/components/ui/slider";
import { Dialog, DialogContent, DialogHeader, DialogTitle, DialogTrigger } from "@/components/ui/dialog";
import { Maximize2, FileJson } from "lucide-react";
import type { VariationWeight } from "@/api/types";
import { JsonEditor } from "@/components/ui/json-editor";
import { cn } from "@/lib/utils";

const RolloutInput = ({ value, onChange, disabled }: { value: number, onChange: (val: number) => void, disabled?: boolean }) => {
    const [local, setLocal] = useState(value.toString());

    useEffect(() => {
        const p = parseFloat(local);
        if (isNaN(p) || p !== value) {
            setLocal(value.toString());
        }
    }, [value]);

    return (
        <input
            type="number"
            step="any"
            disabled={disabled}
            value={local}
            onChange={(e) => {
                setLocal(e.target.value);
                const parsed = parseFloat(e.target.value);
                if (!isNaN(parsed)) {
                    onChange(Math.max(0, Math.min(100, parsed)));
                } else if (e.target.value === '') {
                    onChange(0);
                }
            }}
            onBlur={() => {
                let parsed = parseFloat(local);
                if (isNaN(parsed)) parsed = 0;
                parsed = Math.max(0, Math.min(100, parsed));
                setLocal(parsed.toString());
                onChange(parsed);
            }}
            className="text-right font-mono text-sm bg-transparent border-0 border-b border-dashed border-muted-foreground/50 hover:border-foreground focus:outline-none focus:border-emerald-500 transition-colors p-0 m-0 [&::-webkit-inner-spin-button]:appearance-none disabled:opacity-50 disabled:cursor-not-allowed"
            style={{ width: `${Math.max(2, local.length + 0.5)}ch` }}
        />
    );
};

interface RolloutConfigProps {
    type?: number;
    variations: { id: string; value: string }[];
    rollout: VariationWeight[];
    onChange: (rollout: VariationWeight[]) => void;
    disabled?: boolean;
}

export function RolloutConfig({ type, variations, rollout, onChange, disabled }: RolloutConfigProps) {
    const variationEntries = variations;

    const currentRolloutMap = new Map(rollout.map(r => [r.variationId, r.weight]));

    const handleChange = (variationId: string, weight: number) => {
        weight = Math.max(0, Math.min(100, weight));

        const updated = variationEntries.map((v) => {
            if (v.id === variationId) return { variationId: v.id, weight };
            return { variationId: v.id, weight: currentRolloutMap.get(v.id) || 0 };
        });

        onChange(updated);
    };

    if (type === 0 && variationEntries.length === 2) {
        const trueEntry = variationEntries.find((v) => v.value === 'true');
        const falseEntry = variationEntries.find((v) => v.value === 'false');

        if (trueEntry && falseEntry) {
            const trueId = trueEntry.id;
            const falseId = falseEntry.id;
            const currentTrueWeight = currentRolloutMap.get(trueId) || 0;

            const handleSliderChange = (vals: number[]) => {
                const newTrue = vals[0];
                onChange([
                    { variationId: trueId, weight: newTrue },
                    { variationId: falseId, weight: 100 - newTrue }
                ]);
            };

            return (
                <div className="space-y-4 p-4 border border-border/40 rounded-lg bg-muted/10">
                    <div className="flex items-center justify-between">
                        <Label className="text-sm">Distribution</Label>
                        <span className="text-sm font-medium">{currentTrueWeight}% True</span>
                    </div>
                    <Slider
                        disabled={disabled}
                        value={[currentTrueWeight]}
                        onValueChange={handleSliderChange}
                        max={100}
                        step={1}
                        className="py-2"
                    />
                </div>
            );
        }
    }

    return (
        <div className="space-y-3 p-4 border border-border/40 rounded-lg bg-muted/10">
            <div className="flex items-center justify-between">
                <Label className="text-sm">Distribution</Label>
                {(() => {
                    const rawTotal = variationEntries.reduce((sum, v) => sum + (currentRolloutMap.get(v.id) || 0), 0);
                    const total = Math.round(rawTotal * 10000) / 10000;
                    return (
                        <span className={cn("text-xs font-medium", total === 100 ? "text-emerald-500" : total === 0 ? "text-muted-foreground" : "text-destructive")}>
                            Total: {total}%
                        </span>
                    );
                })()}
            </div>
            <div className="space-y-3">
                {variationEntries.map((v) => (
                    <div key={v.id} className="flex items-center justify-between gap-4 p-2 rounded-md hover:bg-muted/50 transition-colors">
                        <div className="flex items-center gap-2 min-w-0 flex-1">
                            <span className="text-sm font-mono truncate">{v.value}</span>
                            <Dialog>
                                <DialogTrigger asChild>
                                    <button type="button" className="text-muted-foreground hover:text-foreground shrink-0 p-1 rounded-sm hover:bg-muted transition-colors">
                                        <Maximize2 className="h-3 w-3" />
                                    </button>
                                </DialogTrigger>
                                <DialogContent className="sm:max-w-[500px]">
                                    <DialogHeader>
                                        <DialogTitle className="flex items-center gap-2">
                                            {type === 2 ? <FileJson className="h-4 w-4 text-emerald-500" /> : null}
                                            Variation Value
                                        </DialogTitle>
                                    </DialogHeader>
                                    <div className="mt-4 rounded-md overflow-hidden border border-border/40">
                                        {type === 2 ? (
                                            <JsonEditor value={v.value} height="200px" readOnly={true} />
                                        ) : (
                                            <div className="p-4 bg-muted/20 font-mono text-sm max-h-[200px] overflow-auto whitespace-pre-wrap break-all">
                                                {v.value}
                                            </div>
                                        )}
                                    </div>
                                </DialogContent>
                            </Dialog>
                        </div>
                        <div className="flex items-center gap-1 shrink-0">
                            <RolloutInput
                                disabled={disabled}
                                value={currentRolloutMap.get(v.id) || 0}
                                onChange={(val) => handleChange(v.id, val)}
                            />
                            <span className="text-sm text-muted-foreground">%</span>
                        </div>
                    </div>
                ))}
            </div>
        </div>
    );
}
