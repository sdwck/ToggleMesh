export const formatDate = (dateString?: string | null): string => {
    if (!dateString) return 'Unknown';
    try {
        const date = new Date(dateString);
        const pad = (num: number) => String(num).padStart(2, '0');
        
        const year = date.getFullYear();
        const month = pad(date.getMonth() + 1);
        const day = pad(date.getDate());
        const hours = pad(date.getHours());
        const minutes = pad(date.getMinutes());
        const seconds = pad(date.getSeconds());
        
        return `${year}-${month}-${day} ${hours}:${minutes}:${seconds}`;
    } catch {
        return 'Unknown';
    }
};

export const formatProjectDate = (dateString?: string | null): string => {
    if (!dateString) return 'Unknown';
    try {
        const date = new Date(dateString);
        if (date.getFullYear() < 2000) {
            return 'January 1, 0001';
        }
        return date.toLocaleDateString('en-US', {
            year: 'numeric',
            month: 'long',
            day: 'numeric',
        });
    } catch {
        return 'Unknown';
    }
};
