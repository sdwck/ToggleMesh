import Editor, { useMonaco } from '@monaco-editor/react';
import { useEffect, useState } from 'react';
import { cn } from '@/lib/utils';
import { AlertCircle } from 'lucide-react';

interface JsonEditorProps {
    value: string;
    onChange?: (value: string) => void;
    disabled?: boolean;
    readOnly?: boolean;
    className?: string;
    height?: string;
}

export function JsonEditor({ value, onChange, disabled, readOnly, className, height = "250px" }: JsonEditorProps) {
    const monaco = useMonaco();
    const [error, setError] = useState<string | null>(null);

    useEffect(() => {
        if (monaco) {
            (monaco.languages.json as any).jsonDefaults.setDiagnosticsOptions({
                validate: true,
                allowComments: false,
                schemas: [],
                enableSchemaRequest: false,
            });
        }
    }, [monaco]);

    const handleEditorChange = (value: string | undefined) => {
        const newValue = value || '';
        if (onChange) {
            onChange(newValue);
        }
        
        try {
            if (newValue.trim()) {
                JSON.parse(newValue);
            }
            setError(null);
        } catch (e) {
            setError((e as Error).message);
        }
    };

    return (
        <div className={cn("relative group border rounded-md overflow-hidden transition-colors focus-within:border-primary/50", 
                           error ? "border-destructive/50 focus-within:border-destructive" : "border-border/40",
                           className)}>
            <div className={cn("absolute inset-0 bg-background/50 z-10 hidden", disabled && "block")} />
            
            <Editor
                height={height}
                defaultLanguage="json"
                value={value}
                onChange={handleEditorChange}
                theme="vs-dark"
                options={{
                    minimap: { enabled: false },
                    scrollBeyondLastLine: false,
                    fontSize: 13,
                    fontFamily: 'var(--font-mono)',
                    lineNumbers: 'on',
                    renderLineHighlight: 'all',
                    padding: { top: 12, bottom: 12 },
                    wordWrap: 'on',
                    tabSize: 2,
                    formatOnPaste: true,
                    readOnly: disabled || readOnly,
                    contextmenu: false,
                }}
                className="bg-zinc-950"
            />
            
            {error && (
                <div className="absolute bottom-0 left-0 right-0 bg-destructive/10 border-t border-destructive/20 p-2 flex items-center gap-2 text-xs text-destructive backdrop-blur-sm z-20">
                    <AlertCircle className="w-3.5 h-3.5" />
                    <span className="truncate">{error}</span>
                </div>
            )}
        </div>
    );
}
