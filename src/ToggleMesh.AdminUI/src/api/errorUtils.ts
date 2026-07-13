import { type UseFormSetError } from 'react-hook-form';
import { toast } from 'sonner';

export const handleApiError = (error: any, setError: UseFormSetError<any>, defaultMessage: string = 'An error occurred') => {
    const data = error?.response?.data;

    if (!data) {
        toast.error(error.message || defaultMessage);
        return;
    }

    let hasFieldErrors = false;
    let hasRootError = false;

    if (data.errors) {
        if (Array.isArray(data.errors)) {
            data.errors.forEach((err: any) => {
                const fieldName = err.name || 'root';
                if (fieldName.toLowerCase() === 'generalerrors' || fieldName === '') {
                    setError('root', { type: 'server', message: err.reason || err.message });
                    hasRootError = true;
                } else {
                    const camelName = fieldName.charAt(0).toLowerCase() + fieldName.slice(1);
                    setError(camelName, { type: 'server', message: err.reason || err.message });
                    hasFieldErrors = true;
                }
            });
        } else {
            Object.keys(data.errors).forEach((key) => {
                const camelKey = key.charAt(0).toLowerCase() + key.slice(1);
                const message = Array.isArray(data.errors[key]) ? data.errors[key][0] : data.errors[key];

                if (key === '' || key.toLowerCase() === 'generalerrors') {
                    setError('root', { type: 'server', message: message });
                    hasRootError = true;
                } else {
                    setError(camelKey, { type: 'server', message: message });
                    hasFieldErrors = true;
                }
            });
        }
    }

    if (!hasFieldErrors && !hasRootError && data.message) {
        setError('root', { type: 'server', message: data.message });
    } else if (!hasFieldErrors && !hasRootError && data.title) {
        setError('root', { type: 'server', message: data.title });
    } else if (!hasFieldErrors && !hasRootError) {
        toast.error(defaultMessage);
    }
};

export const toastApiError = (error: any, defaultMessage: string = 'An error occurred') => {
    const data = error?.response?.data;
    if (!data) {
        toast.error(error.message || defaultMessage);
        return;
    }
    if (data.errors) {
        if (Array.isArray(data.errors)) {
            toast.error(data.errors[0]?.reason || data.errors[0]?.message || defaultMessage);
            return;
        } else {
            const firstKey = Object.keys(data.errors)[0];
            const msg = Array.isArray(data.errors[firstKey]) ? data.errors[firstKey][0] : data.errors[firstKey];
            if (msg) {
                toast.error(msg);
                return;
            }
        }
    }
    if (data.message) {
        toast.error(data.message);
    } else if (data.title) {
        toast.error(data.title);
    } else {
        toast.error(defaultMessage);
    }
};
