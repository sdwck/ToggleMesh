import { useState, useEffect, useRef } from "react";
import { Button } from "@/components/ui/button";
import { Input } from "@/components/ui/input";
import { JsonEditor } from "@/components/ui/json-editor";
import { Plus, Trash2, GripVertical, FileJson } from "lucide-react";

const uuidv4 = () => crypto.randomUUID();

export interface Variation {
    id: string;
    value: string;
}

interface VariationsManagerProps {
    variations: Variation[];
    onChange: (variations: Variation[]) => void;
    type: string;
    originalVariationIds?: string[];
}

export function VariationsManager({ variations, onChange, type, originalVariationIds }: VariationsManagerProps) {
    const containerRef = useRef<HTMLDivElement>(null);
    const prevLen = useRef(variations.length);

    useEffect(() => {
        if (variations.length > prevLen.current) {
            setTimeout(() => {
                if (containerRef.current) {
                    const inputs = containerRef.current.querySelectorAll('input');
                    if (inputs.length > 0) {
                        inputs[inputs.length - 1].focus();
                    }
                }
            }, 10);
        }
        prevLen.current = variations.length;
    }, [variations.length]);

    const handleAdd = () => {
        onChange([...variations, { id: uuidv4(), value: "" }]);
    };

    const handleRemove = (id: string) => {
        if (variations.length <= 1) return;
        onChange(variations.filter(v => v.id !== id));
    };

    const handleChange = (id: string, value: string) => {
        onChange(variations.map(v => v.id === id ? { ...v, value } : v));
    };

    const [draggedIndex, setDraggedIndex] = useState<number | null>(null);

    const handleDragStart = (e: React.DragEvent<HTMLDivElement>, index: number) => {
        setDraggedIndex(index);
        e.dataTransfer.effectAllowed = 'move';
        e.dataTransfer.setData('text/plain', index.toString());
    };

    const handleDragOver = (e: React.DragEvent<HTMLDivElement>) => {
        e.preventDefault();
        e.dataTransfer.dropEffect = 'move';
    };

    const handleDrop = (e: React.DragEvent<HTMLDivElement>, index: number) => {
        e.preventDefault();
        if (draggedIndex === null || draggedIndex === index) return;

        const newVariations = [...variations];
        const draggedItem = newVariations[draggedIndex];
        newVariations.splice(draggedIndex, 1);
        newVariations.splice(index, 0, draggedItem);

        onChange(newVariations);
        setDraggedIndex(null);
    };

    const handleDragEnd = () => {
        setDraggedIndex(null);
    };

    return (
        <div className="space-y-3">
            <div className="flex justify-between items-center mb-1">
                <span className="text-sm font-medium">Variations ({type})</span>
                {type === "JSON" && (
                    <span className="text-xs text-muted-foreground flex items-center gap-1">
                        <FileJson className="w-3 h-3" /> Valid JSON required
                    </span>
                )}
            </div>

            <div className="space-y-2" ref={containerRef}>
                {variations.map((v, index) => {
                    const isOriginal = originalVariationIds?.includes(v.id);
                    return (
                        <div
                            key={v.id}
                            className={`flex items-start gap-2 p-2 rounded-md transition-colors ${draggedIndex === index ? 'opacity-50' : 'bg-card'}`}
                            draggable
                            onDragStart={(e) => handleDragStart(e, index)}
                            onDragOver={handleDragOver}
                            onDrop={(e) => handleDrop(e, index)}
                            onDragEnd={handleDragEnd}
                        >
                            <div className="pt-2 cursor-grab active:cursor-grabbing text-muted-foreground shrink-0 mt-0.5">
                                <GripVertical className="h-4 w-4" />
                            </div>
                            <div className="flex-1 min-w-0">
                                {type === 'JSON' ? (
                                    <div className="relative group">
                                        {draggedIndex !== null && (
                                            <div className="h-[120px] w-full bg-muted/20 border border-dashed rounded-md flex items-center justify-center">
                                                <span className="text-muted-foreground text-sm font-mono opacity-50">Dragging...</span>
                                            </div>
                                        )}
                                        <div style={{ display: draggedIndex !== null ? 'none' : 'block' }}>
                                            <JsonEditor
                                                key={`${v.id}-${index}`}
                                                value={v.value}
                                                onChange={(val) => {
                                                    if (!isOriginal) handleChange(v.id, val);
                                                }}
                                                height="120px"
                                            />
                                            {isOriginal && (
                                                <div className="absolute inset-0 bg-background/50 cursor-not-allowed flex items-center justify-center rounded-md border border-border/50">
                                                    <span className="bg-background px-2 py-1 rounded text-xs text-muted-foreground shadow-sm">Immutable</span>
                                                </div>
                                            )}
                                        </div>
                                    </div>
                                ) : (
                                    <Input
                                        value={v.value}
                                        onChange={(e) => handleChange(v.id, e.target.value)}
                                        placeholder={`Variation ${index + 1} value...`}
                                        className="font-mono text-sm"
                                        disabled={isOriginal}
                                        title={isOriginal ? "Existing variation values cannot be modified." : ""}
                                    />
                                )}
                            </div>
                            <Button
                                variant="ghost"
                                size="icon"
                                onClick={() => handleRemove(v.id)}
                                disabled={variations.length <= 1}
                                className="text-muted-foreground hover:text-destructive shrink-0 ml-1"
                                type="button"
                            >
                                <Trash2 className="h-4 w-4" />
                            </Button>
                        </div>
                    )
                })}
            </div>

            <div className="flex gap-2">
                <Button
                    variant="outline"
                    size="sm"
                    onClick={handleAdd}
                    className="w-full mt-2 border-dashed"
                    type="button"
                >
                    <Plus className="h-4 w-4 mr-2" /> Add Variation
                </Button>
            </div>
        </div>
    );
}
