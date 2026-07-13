export interface RuleOperator {
    readonly name: string;
    compile(ruleValue: string): any;
    evaluate(userValue: string, compiledRuleValue: any): boolean;
}

export class EqualsOperator implements RuleOperator {
    name = "Equals";
    compile(ruleValue: string) { return ruleValue; }
    evaluate(userValue: string, compiledRuleValue: any) { return userValue === compiledRuleValue; }
}

export class NotEqualsOperator implements RuleOperator {
    name = "NotEquals";
    compile(ruleValue: string) { return ruleValue; }
    evaluate(userValue: string, compiledRuleValue: any) { return userValue !== compiledRuleValue; }
}

export class ContainsOperator implements RuleOperator {
    name = "Contains";
    compile(ruleValue: string) { return ruleValue; }
    evaluate(userValue: string, compiledRuleValue: any) { return userValue.includes(String(compiledRuleValue)); }
}

export class StartsWithOperator implements RuleOperator {
    name = "StartsWith";
    compile(ruleValue: string) { return ruleValue; }
    evaluate(userValue: string, compiledRuleValue: any) { return userValue.startsWith(String(compiledRuleValue)); }
}

export class EndsWithOperator implements RuleOperator {
    name = "EndsWith";
    compile(ruleValue: string) { return ruleValue; }
    evaluate(userValue: string, compiledRuleValue: any) { return userValue.endsWith(String(compiledRuleValue)); }
}

export class InListOperator implements RuleOperator {
    name = "InList";
    compile(ruleValue: string): Set<string> {
        return new Set((ruleValue || "").split(",").map(x => x.trim()));
    }
    evaluate(userValue: string, compiledRuleValue: any) {
        return (compiledRuleValue as Set<string>).has(userValue);
    }
}

export class RegexOperator implements RuleOperator {
    name = "Regex";
    compile(ruleValue: string) {
        try {
            return new RegExp(ruleValue);
        } catch {
            return null;
        }
    }
    evaluate(userValue: string, compiledRuleValue: any) {
        if (!compiledRuleValue) return false;
        return (compiledRuleValue as RegExp).test(userValue);
    }
}

export class GreaterThanOperator implements RuleOperator {
    name = "GreaterThan";
    compile(ruleValue: string) { return parseFloat(ruleValue); }
    evaluate(userValue: string, compiledRuleValue: any) {
        if (isNaN(compiledRuleValue)) return false;
        const uv = parseFloat(userValue);
        if (isNaN(uv)) return false;
        return uv > compiledRuleValue;
    }
}

export class GreaterThanOrEqualOperator implements RuleOperator {
    name = "GreaterThanOrEqual";
    compile(ruleValue: string) { return parseFloat(ruleValue); }
    evaluate(userValue: string, compiledRuleValue: any) {
        if (isNaN(compiledRuleValue)) return false;
        const uv = parseFloat(userValue);
        if (isNaN(uv)) return false;
        return uv >= compiledRuleValue;
    }
}

export class LessThanOperator implements RuleOperator {
    name = "LessThan";
    compile(ruleValue: string) { return parseFloat(ruleValue); }
    evaluate(userValue: string, compiledRuleValue: any) {
        if (isNaN(compiledRuleValue)) return false;
        const uv = parseFloat(userValue);
        if (isNaN(uv)) return false;
        return uv < compiledRuleValue;
    }
}

export class LessThanOrEqualOperator implements RuleOperator {
    name = "LessThanOrEqual";
    compile(ruleValue: string) { return parseFloat(ruleValue); }
    evaluate(userValue: string, compiledRuleValue: any) {
        if (isNaN(compiledRuleValue)) return false;
        const uv = parseFloat(userValue);
        if (isNaN(uv)) return false;
        return uv <= compiledRuleValue;
    }
}

function compareSemver(a: string, b: string): number {
    const pa = a.split('.').map(Number);
    const pb = b.split('.').map(Number);
    for (let i = 0; i < Math.max(pa.length, pb.length); i++) {
        const na = pa[i] || 0;
        const nb = pb[i] || 0;
        if (na > nb) return 1;
        if (na < nb) return -1;
    }
    return 0;
}

export class SemVerEqualOperator implements RuleOperator {
    name = "SemVerEqual";
    compile(ruleValue: string) { return ruleValue.replace(/^v/, ''); }
    evaluate(userValue: string, compiledRuleValue: any) {
        return compareSemver(userValue.replace(/^v/, ''), compiledRuleValue) === 0;
    }
}

export class SemVerGreaterThanOperator implements RuleOperator {
    name = "SemVerGreaterThan";
    compile(ruleValue: string) { return ruleValue.replace(/^v/, ''); }
    evaluate(userValue: string, compiledRuleValue: any) {
        return compareSemver(userValue.replace(/^v/, ''), compiledRuleValue) > 0;
    }
}

export class SemVerLessThanOperator implements RuleOperator {
    name = "SemVerLessThan";
    compile(ruleValue: string) { return ruleValue.replace(/^v/, ''); }
    evaluate(userValue: string, compiledRuleValue: any) {
        return compareSemver(userValue.replace(/^v/, ''), compiledRuleValue) < 0;
    }
}

export class SemVerGreaterThanOrEqualOperator implements RuleOperator {
    name = "SemVerGreaterThanOrEqual";
    compile(ruleValue: string) { return ruleValue.replace(/^v/, ''); }
    evaluate(userValue: string, compiledRuleValue: any) {
        return compareSemver(userValue.replace(/^v/, ''), compiledRuleValue) >= 0;
    }
}

export class SemVerLessThanOrEqualOperator implements RuleOperator {
    name = "SemVerLessThanOrEqual";
    compile(ruleValue: string) { return ruleValue.replace(/^v/, ''); }
    evaluate(userValue: string, compiledRuleValue: any) {
        return compareSemver(userValue.replace(/^v/, ''), compiledRuleValue) <= 0;
    }
}

export class FalseOperator implements RuleOperator {
    name = "False";
    compile(ruleValue: string) { return null; }
    evaluate(userValue: string, compiledRuleValue: any) { return false; }
}

export class InSegmentOperator implements RuleOperator {
    name = "InSegment";
    compile(ruleValue: string) { return ruleValue; }
    evaluate(userValue: string, compiledRuleValue: any): boolean {
        throw new Error("InSegment operator should be evaluated directly by RuleEngine");
    }
}

export const OPERATOR_MAP: Record<string, RuleOperator> = (() => {
    const map: Record<string, RuleOperator> = {};
    const ops = [
        new EqualsOperator(),
        new NotEqualsOperator(),
        new ContainsOperator(),
        new StartsWithOperator(),
        new EndsWithOperator(),
        new InListOperator(),
        new RegexOperator(),
        new GreaterThanOperator(),
        new GreaterThanOrEqualOperator(),
        new LessThanOperator(),
        new LessThanOrEqualOperator(),
        new SemVerEqualOperator(),
        new SemVerGreaterThanOperator(),
        new SemVerGreaterThanOrEqualOperator(),
        new SemVerLessThanOperator(),
        new SemVerLessThanOrEqualOperator()
    ];
    for (const op of ops) {
        map[op.name.toLowerCase()] = op;
    }
    return map;
})();
