import { type UseFormSetError } from 'react-hook-form';
import { toast } from 'sonner';

export const handleApiError = (error: any, setError: UseFormSetError<any>, defaultMessage: string = 'An error occurred') => {
    if (!error?.response) {
        if (error?.message === 'Network Error' || error?.code === 'ERR_NETWORK' || error?.code === 'ERR_CONNECTION_REFUSED') {
            toast.error('Unable to connect to the server. Please check your internet connection or try again later.');
            setError('root', { type: 'server', message: 'Unable to connect to the server.' });
        } else {
            toast.error(error?.message || defaultMessage);
            setError('root', { type: 'server', message: error?.message || defaultMessage });
        }
        return;
    }

    const data = error.response.data;
    if (!data) {
        toast.error(defaultMessage);
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
    if (!error?.response) {
        if (error?.message === 'Network Error' || error?.code === 'ERR_NETWORK' || error?.code === 'ERR_CONNECTION_REFUSED') {
            toast.error('Unable to connect to the server. Please check your internet connection or try again later.');
        } else {
            toast.error(error?.message || defaultMessage);
        }
        return;
    }

    const data = error.response.data;
    if (!data) {
        toast.error(defaultMessage);
        return;
    }

    if (data.errors) {
        if (Array.isArray(data.errors) && data.errors.length > 0) {
            const firstErr = data.errors[0];
            const msg = typeof firstErr === 'string' ? firstErr : (firstErr?.reason || firstErr?.message);
            if (msg) {
                toast.error(msg);
                return;
            }
        } else if (typeof data.errors === 'object') {
            const keys = Object.keys(data.errors);
            if (keys.length > 0) {
                const firstKey = keys[0];
                const val = data.errors[firstKey];
                const msg = Array.isArray(val) ? val[0] : (typeof val === 'string' ? val : val?.message || val?.reason);
                if (msg) {
                    toast.error(msg);
                    return;
                }
            }
        }
    }

    let msg = data.message || data.title || data.detail;

    if (typeof msg === 'string' && (msg.includes('One or more') || msg.toLowerCase().includes('validation error'))) {
        if (data.detail) {
            msg = data.detail;
        } else {
            msg = defaultMessage;
        }
    }

    if (msg && typeof msg === 'string') {
        toast.error(msg);
    } else {
        toast.error(defaultMessage);
    }
};
