export const getEnvBadgeStyle = (name: string): string => {
    if (!name) return "bg-zinc-500/10 text-zinc-400 border-zinc-500/20";
    
    const lower = name.toLowerCase();
    if (lower.includes('prod') || lower.includes('prd')) {
        return "bg-rose-500/10 text-rose-400 border-rose-500/20";
    }
    if (lower.includes('dev') || lower.includes('local')) {
        return "bg-emerald-500/10 text-emerald-400 border-emerald-500/20";
    }
    if (lower.includes('stg') || lower.includes('stage') || lower.includes('test') || lower.includes('qa')) {
        return "bg-amber-500/10 text-amber-400 border-amber-500/20";
    }
    return "bg-blue-500/10 text-blue-400 border-blue-500/20";
};
