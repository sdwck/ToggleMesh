import { FeatureFlagDto, RuleDto, SegmentDto, VariationWeight } from './models.js';
import { RuleOperator, OPERATOR_MAP, FalseOperator, InSegmentOperator } from './operators.js';

export class CompiledRule {
    constructor(
        public attribute: string,
        public operator: RuleOperator,
        public compiledValue: any
    ) { }
}

export class CompiledRuleGroup {
    constructor(
        public rules: CompiledRule[],
        public priority: number,
        public rollout: VariationWeight[]
    ) { }
}

export class CachedFlag {
    key: string;
    isEnabled: boolean;
    contextualRollouts?: Record<string, VariationWeight[]>;
    isExperimentActive: boolean;
    groups: CompiledRuleGroup[];
    originalDto: FeatureFlagDto;
    parsedContextualRollouts: Record<string, VariationWeight[]> = {};

    constructor(dto: FeatureFlagDto, groups: CompiledRuleGroup[]) {
        this.key = dto.key;
        this.isEnabled = dto.isEnabled;
        this.contextualRollouts = dto.contextualRollouts;
        this.isExperimentActive = dto.isExperimentActive;
        this.groups = groups;
        this.originalDto = dto;

        if (dto.contextualRollouts && dto.contextPartitionKeys) {
            for (const [k, v] of Object.entries(dto.contextualRollouts)) {
                try {
                    const d = JSON.parse(k);
                    const parts = [];
                    for (const key of dto.contextPartitionKeys) {
                        parts.push(String(d[key] ?? "null"));
                    }
                    const sliceKey = parts.join("|");
                    this.parsedContextualRollouts[sliceKey] = v;
                } catch {
                    // ignore
                }
            }
        }
    }
}

export class CachedSegment {
    id: string;
    name: string;
    groups: CompiledRuleGroup[];

    constructor(dto: SegmentDto, groups: CompiledRuleGroup[]) {
        this.id = dto.id;
        this.name = dto.name;
        this.groups = groups;
    }
}

export interface ISegmentProvider {
    getSegmentRules(segmentId: string): CompiledRuleGroup[] | null;
}

export class RuleEngine {
    constructor(private segmentProvider?: ISegmentProvider) { }

    compileRules(rules: RuleDto[]): CompiledRuleGroup[] {
        if (!rules || rules.length === 0) {
            return [];
        }

        const groupsDict: Record<number, RuleDto[]> = {};
        for (const r of rules) {
            if (!groupsDict[r.groupId]) {
                groupsDict[r.groupId] = [];
            }
            groupsDict[r.groupId].push(r);
        }

        const compiledGroups: CompiledRuleGroup[] = [];
        for (const gId of Object.keys(groupsDict)) {
            const gRules = groupsDict[Number(gId)];
            const compiledRules: CompiledRule[] = [];

            for (const r of gRules) {
                let op: RuleOperator;
                if (r.operator.toLowerCase() === "insegment") {
                    op = new InSegmentOperator();
                } else {
                    op = OPERATOR_MAP[r.operator.toLowerCase()] || new FalseOperator();
                }

                compiledRules.push(new CompiledRule(
                    r.attribute,
                    op,
                    op.compile(r.value)
                ));
            }

            compiledGroups.push(new CompiledRuleGroup(compiledRules, gRules[0].priority, gRules[0].rollout || []));
        }

        compiledGroups.sort((a, b) => a.priority - b.priority);

        return compiledGroups;
    }

    evaluate(groups: CompiledRuleGroup[], context: Record<string, string>): CompiledRuleGroup | null {
        if (!groups || groups.length === 0) {
            return null;
        }

        for (const group of groups) {
            let groupPassed = true;
            for (const rule of group.rules) {
                if (rule.operator instanceof InSegmentOperator) {
                    if (this.segmentProvider) {
                        const segmentRules = this.segmentProvider.getSegmentRules(rule.compiledValue);
                        if (segmentRules && this.evaluate(segmentRules, context)) {
                            continue;
                        }
                    }
                } else {
                    const userValue = context[rule.attribute];
                    if (userValue !== undefined && userValue !== null) {
                        if (rule.operator.evaluate(String(userValue), rule.compiledValue)) {
                            continue;
                        }
                    }
                }

                groupPassed = false;
                break;
            }

            if (groupPassed) {
                return group;
            }
        }

        return null;
    }
}
