import { Badge } from '@/components/ui/badge';
import { CheckCircle2, XCircle, Sliders, Users, ArrowRight, Layers } from 'lucide-react';
import type { SegmentDto } from '@/api/types';

interface ChangeInstructionsRendererProps {
    jsonString: string;
    diffSummaryJson?: string;
    variations?: { id: string; value: string }[];
    segments?: SegmentDto[];
    showRaw?: boolean;
}

export function ChangeInstructionsRenderer({ jsonString, diffSummaryJson, variations, segments, showRaw = false }: ChangeInstructionsRendererProps) {
    let patch: any = null;
    let diff: any = null;
    try {
        patch = JSON.parse(jsonString || '{}');
        diff = diffSummaryJson ? JSON.parse(diffSummaryJson) : null;
    } catch {
        return <pre className="text-[11px] font-mono text-amber-400">{jsonString}</pre>;
    }

    if (!patch || Object.keys(patch).length === 0) {
        return <span className="text-xs text-muted-foreground italic">No specific changes detailed</span>;
    }

    const resolveVariationName = (varId: string) => {
        if (!varId) return 'N/A';
        if (variations && variations.length > 0) {
            const found = variations.find(v => v.id === varId || v.value === varId);
            if (found) return found.value;
        }
        return varId;
    };

    const resolveSegmentName = (val: string) => {
        if (!val) return 'N/A';
        if (segments && segments.length > 0) {
            const found = segments.find(s => s.id === val || s.name === val);
            if (found) return found.name;
        }
        return val;
    };

    const formatWeight = (rawWeight: number) => {
        if (rawWeight === undefined || rawWeight === null) return '0%';
        const val = rawWeight > 100 ? rawWeight / 100 : rawWeight;
        const formatted = val.toFixed(2).replace(/\.00$/, '').replace(/(\.\d)0$/, '$1');
        return `${formatted}%`;
    };

    const formatRollout = (rolloutArr: any[]) => {
        if (!Array.isArray(rolloutArr) || rolloutArr.length === 0) return null;
        return rolloutArr.map((r) => {
            const name = resolveVariationName(r.variationId);
            return `${name}: ${formatWeight(r.weight)}`;
        }).join(', ');
    };

    const hasIsEnabled = patch.isEnabled !== undefined && patch.isEnabled !== null;
    const hasRules = Array.isArray(patch.rules);
    const hasFallthrough = Array.isArray(patch.fallthroughRollout);
    const hasTargets = patch.individualTargets && typeof patch.individualTargets === 'object' && Object.keys(patch.individualTargets).length > 0;

    let addedRules: any[] = [];
    let modifiedRules: any[] = [];
    let deletedRules: any[] = [];
    let legacyRules: any[] = [];

    const mapRule = (r: any, idx: number) => ({
        ...r,
        _renderIdx: idx,
        groupId: r.groupId ?? r.GroupId,
        attribute: r.attribute ?? r.Attribute,
        operator: r.operator ?? r.Operator,
        value: r.value ?? r.Value,
        rollout: (r.rollout ?? r.Rollout)?.map((ro: any) => ({
            variationId: ro.variationId ?? ro.VariationId,
            weight: ro.weight ?? ro.Weight
        }))
    });

    if (diff) {
        addedRules = (diff.addedRules || diff.AddedRules || []).map(mapRule);
        modifiedRules = (diff.modifiedRules || diff.ModifiedRules || []).map(mapRule);
        deletedRules = (diff.deletedRules || diff.DeletedRules || []).map(mapRule);
    } else {
        const hasOldRules = Array.isArray(patch._oldRules);
        if (hasRules && hasOldRules) {
            const oldMap = new Map();
            patch._oldRules.forEach((r: any, idx: number) => {
                const key = r.groupId || `idx_${idx}`;
                oldMap.set(key, r);
            });

            const currentMap = new Map();
            patch.rules.forEach((r: any, idx: number) => {
                const key = r.groupId || `idx_${idx}`;
                currentMap.set(key, r);
                
                const oldRule = oldMap.get(key);
                if (!oldRule) {
                    addedRules.push({ ...r, _renderIdx: idx });
                } else if (JSON.stringify(r) !== JSON.stringify(oldRule)) {
                    modifiedRules.push({ ...r, _renderIdx: idx });
                }
            });

            patch._oldRules.forEach((r: any, idx: number) => {
                const key = r.groupId || `idx_${idx}`;
                if (!currentMap.has(key)) {
                    deletedRules.push({ ...r, _renderIdx: idx });
                }
            });
        } else if (hasRules) {
            legacyRules = patch.rules.map((r: any, idx: number) => ({ ...r, _renderIdx: idx }));
        }
    }

    const renderRule = (rule: any, status: 'added' | 'modified' | 'deleted' | 'legacy') => {
        const isSegment = rule.operator === 'InSegment' || rule.operator === 'IN_SEGMENT';
        const valueDisplay = isSegment ? resolveSegmentName(rule.value) : rule.value;
        const rolloutStr = formatRollout(rule.rollout);

        let borderClass = 'border-zinc-800/80 bg-zinc-950/60';
        if (status === 'added') borderClass = 'border-emerald-500/40 bg-emerald-500/10';
        else if (status === 'deleted') borderClass = 'border-rose-500/40 bg-rose-500/10 opacity-70';

        return (
            <div key={rule.groupId || rule._renderIdx} className={`flex flex-wrap items-center gap-2 text-zinc-300 font-mono text-[11px] p-2 rounded border ${borderClass}`}>
                <span className="text-zinc-500 font-sans text-[10px]">#{rule.priority ?? (rule._renderIdx + 1)}</span>
                <span className={`${status === 'deleted' ? 'text-rose-400' : 'text-blue-400'} font-bold`}>IF</span>
                <span className="text-zinc-200">{isSegment ? 'User Context' : (rule.attribute || 'Property')}</span>
                <span className={`${status === 'deleted' ? 'text-rose-400' : 'text-blue-400'} font-semibold`}>{rule.operator || '='}</span>
                <span className={`${status === 'deleted' ? 'text-rose-300 bg-rose-500/10 border-rose-500/20' : 'text-emerald-300 bg-emerald-500/10 border-emerald-500/20'} px-1.5 py-0.5 rounded border font-semibold`}>
                    "{valueDisplay}"
                </span>
                {rolloutStr && (
                    <>
                        <ArrowRight className="w-3 h-3 text-zinc-500 shrink-0" />
                        <span className="text-purple-300 bg-purple-500/10 px-1.5 py-0.5 rounded border border-purple-500/20 font-semibold">
                            {rolloutStr}
                        </span>
                    </>
                )}
                {status === 'deleted' && <span className="text-xs text-rose-400 font-bold ml-2">(DELETED)</span>}
            </div>
        );
    };

    return (
        <div className="space-y-3">
            <div className="flex flex-wrap items-center gap-2">
                {hasIsEnabled && (
                    patch.isEnabled ? (
                        <Badge variant="outline" className="border-emerald-500/40 bg-emerald-500/10 text-emerald-400 gap-1 text-xs">
                            <CheckCircle2 className="w-3 h-3" /> Enable Flag
                        </Badge>
                    ) : (
                        <Badge variant="outline" className="border-rose-500/40 bg-rose-500/10 text-rose-400 gap-1 text-xs">
                            <XCircle className="w-3 h-3" /> Disable Flag
                        </Badge>
                    )
                )}

                {addedRules.length > 0 && (
                    <Badge variant="outline" className="border-emerald-500/40 bg-emerald-500/10 text-emerald-300 gap-1 text-xs">
                        <Sliders className="w-3 h-3" /> Added Rules ({addedRules.length})
                    </Badge>
                )}
                {modifiedRules.length > 0 && (
                    <Badge variant="outline" className="border-blue-500/40 bg-blue-500/10 text-blue-300 gap-1 text-xs">
                        <Sliders className="w-3 h-3" /> Modified Rules ({modifiedRules.length})
                    </Badge>
                )}
                {deletedRules.length > 0 && (
                    <Badge variant="outline" className="border-rose-500/40 bg-rose-500/10 text-rose-300 gap-1 text-xs">
                        <Sliders className="w-3 h-3" /> Deleted Rules ({deletedRules.length})
                    </Badge>
                )}
                {legacyRules.length > 0 && (
                    <Badge variant="outline" className="border-blue-500/40 bg-blue-500/10 text-blue-300 gap-1 text-xs">
                        <Sliders className="w-3 h-3" /> Updated Rules ({legacyRules.length})
                    </Badge>
                )}

                {hasFallthrough && (
                    <Badge variant="outline" className="border-purple-500/40 bg-purple-500/10 text-purple-300 gap-1 text-xs">
                        <Sliders className="w-3 h-3" /> Modified Default Rollout
                    </Badge>
                )}

                {hasTargets && (
                    <Badge variant="outline" className="border-amber-500/40 bg-amber-500/10 text-amber-300 gap-1 text-xs">
                        <Users className="w-3 h-3" /> Updated Targets ({Object.keys(patch.individualTargets).length})
                    </Badge>
                )}
            </div>

            {(diff ? (addedRules.length > 0 || modifiedRules.length > 0 || deletedRules.length > 0) : (hasRules && (addedRules.length > 0 || modifiedRules.length > 0 || deletedRules.length > 0 || legacyRules.length > 0))) && (
                <div className="text-xs bg-zinc-900/60 p-3 rounded-lg border border-border/30 space-y-2">
                    <div className="font-semibold text-zinc-300 text-[11px] uppercase tracking-wider flex items-center gap-1.5">
                        <Sliders className="w-3 h-3 text-blue-400" /> Targeting Rules
                    </div>
                    <div className="space-y-1.5">
                        {addedRules.map(r => renderRule(r, 'added'))}
                        {modifiedRules.map(r => renderRule(r, 'modified'))}
                        {deletedRules.map(r => renderRule(r, 'deleted'))}
                        {legacyRules.map(r => renderRule(r, 'legacy'))}
                    </div>
                </div>
            )}

            {hasFallthrough && patch.fallthroughRollout.length > 0 && (
                <div className="text-xs bg-zinc-900/60 p-3 rounded-lg border border-border/30 space-y-1.5">
                    <div className="font-semibold text-zinc-300 text-[11px] uppercase tracking-wider flex items-center gap-1.5">
                        <Layers className="w-3 h-3 text-purple-400" /> Default Rollout Distribution
                    </div>
                    <div className="flex flex-wrap items-center gap-2 font-mono text-[11px]">
                        {patch.fallthroughRollout.map((item: any, idx: number) => (
                            <div key={idx} className="bg-purple-500/10 text-purple-300 px-2 py-1 rounded border border-purple-500/20 flex items-center gap-1.5">
                                <span className="text-zinc-300">{resolveVariationName(item.variationId)}:</span>
                                <span className="font-bold text-purple-200">{formatWeight(item.weight)}</span>
                            </div>
                        ))}
                    </div>
                </div>
            )}

            {hasTargets && (
                <div className="text-xs bg-zinc-900/60 p-3 rounded-lg border border-border/30 space-y-1.5">
                    <div className="font-semibold text-zinc-300 text-[11px] uppercase tracking-wider flex items-center gap-1.5">
                        <Users className="w-3 h-3 text-amber-400" /> Individual User Targets
                    </div>
                    <div className="flex flex-wrap items-center gap-2 font-mono text-[11px]">
                        {Object.entries(patch.individualTargets).map(([userId, varId]: [string, any], idx: number) => (
                            <div key={idx} className="bg-amber-500/10 text-amber-300 px-2 py-1 rounded border border-amber-500/20 flex items-center gap-1.5">
                                <span className="text-zinc-300">{userId}</span>
                                <ArrowRight className="w-2.5 h-2.5 text-zinc-500" />
                                <span className="font-bold text-amber-200">{resolveVariationName(varId)}</span>
                            </div>
                        ))}
                    </div>
                </div>
            )}



            {showRaw && (
                <div className="bg-zinc-950 p-3 rounded border border-border/30 overflow-x-auto font-mono text-[11px] text-zinc-300 mt-2">
                    <pre>{JSON.stringify(patch, null, 2)}</pre>
                </div>
            )}
        </div>
    );
}
