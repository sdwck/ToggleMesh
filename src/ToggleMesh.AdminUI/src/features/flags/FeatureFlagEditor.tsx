import { useEffect } from 'react';
import { useForm, useFieldArray } from 'react-hook-form';
import { zodResolver } from '@hookform/resolvers/zod';
import { z } from 'zod';
import { Plus, Trash2, Settings2 } from 'lucide-react';
import { useUpdateFeatureFlag } from '@/api/queries';
import type { FeatureFlag } from '@/api/types';
import { Sheet, SheetContent, SheetHeader, SheetTitle, SheetDescription, SheetFooter } from '@/components/ui/sheet';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Switch } from '@/components/ui/switch';
import { Slider } from '@/components/ui/slider';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Separator } from '@/components/ui/separator';
import { toast } from 'sonner';

const ruleSchema = z.object({
  groupId: z.number(),
  attribute: z.string().min(1, 'Attribute is required'),
  operator: z.string().min(1, 'Operator is required'),
  value: z.string().min(1, 'Value is required'),
});

const formSchema = z.object({
  isEnabled: z.boolean(),
  rolloutPercentage: z.number().nullable(),
  isRolloutEnabled: z.boolean(),
  rules: z.array(ruleSchema),
});

type FormValues = z.infer<typeof formSchema>;

const operators = [
  'Equals', 'NotEquals', 'Contains', 'StartsWith', 'EndsWith',
  'GreaterThan', 'LessThan', 'InList', 'Regex', 'SemVerEqual', 'SemVerGreaterThan'
];

interface FeatureFlagEditorProps {
  flag: FeatureFlag | null;
  projectId: string;
  envId: string;
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

export function FeatureFlagEditor({ flag, projectId, envId, open, onOpenChange }: FeatureFlagEditorProps) {
  const updateFlag = useUpdateFeatureFlag(projectId, envId, flag?.key || '');

  const form = useForm<FormValues>({
    resolver: zodResolver(formSchema),
    defaultValues: {
      isEnabled: false,
      isRolloutEnabled: false,
      rolloutPercentage: 0,
      rules: [],
    },
  });

  const { fields, append, remove } = useFieldArray({
    control: form.control,
    name: 'rules',
  });

  useEffect(() => {
    if (flag && open) {
      form.reset({
        isEnabled: flag.isEnabled,
        isRolloutEnabled: flag.rolloutPercentage !== null,
        rolloutPercentage: flag.rolloutPercentage || 0,
        rules: flag.rules || [],
      });
    }
  }, [flag, open, form]);

  const onSubmit = async (values: FormValues) => {
    if (!flag) return;
    try {
      await updateFlag.mutateAsync({
        isEnabled: values.isEnabled,
        rolloutPercentage: values.isRolloutEnabled ? values.rolloutPercentage : null,
        rules: values.rules,
      });
      toast.success('Feature flag updated');
      onOpenChange(false);
    } catch {
      toast.error('Failed to update feature flag');
    }
  };

  const addConditionOR = () => {
    const maxGroupId = fields.length > 0 ? Math.max(...fields.map(f => f.groupId)) : -1;
    append({ groupId: maxGroupId + 1, attribute: '', operator: 'Equals', value: '' });
  };

  const addRuleAND = (groupId: number) => {
    append({ groupId, attribute: '', operator: 'Equals', value: '' });
  };

  const groupedRules = fields.reduce((acc, field, index) => {
    if (!acc[field.groupId]) {
      acc[field.groupId] = [];
    }
    acc[field.groupId].push({ ...field, index });
    return acc;
  }, {} as Record<number, (typeof fields[0] & { index: number })[]>);

  if (!flag) return null;

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent className="w-full sm:max-w-2xl overflow-y-auto">
        <SheetHeader>
          <SheetTitle className="font-mono">{flag.key}</SheetTitle>
          <SheetDescription>Configure targeting rules and rollout strategy.</SheetDescription>
        </SheetHeader>

        <form onSubmit={form.handleSubmit(onSubmit)} className="space-y-8 mt-8">
          <div className="flex items-center justify-between px-6 py-4 border border-border/40 rounded-lg bg-muted/20">
            <div className="space-y-0.5">
              <Label className="text-base">Enable Flag</Label>
              <p className="text-sm text-muted-foreground">Serve this flag to users.</p>
            </div>
            <Switch
              checked={form.watch('isEnabled')}
              onCheckedChange={(val) => form.setValue('isEnabled', val)}
            />
          </div>

          <div className="space-y-4 px-2">
            <div className="flex items-center gap-2">
              <Switch
                checked={form.watch('isRolloutEnabled')}
                onCheckedChange={(val) => form.setValue('isRolloutEnabled', val)}
              />
              <Label>Incremental Rollout</Label>
            </div>
            
            {form.watch('isRolloutEnabled') && (
              <div className="pl-12 pr-4 space-y-4">
                <div className="flex items-center justify-between">
                  <span className="text-sm text-muted-foreground">Percentage</span>
                  <span className="text-sm font-medium">{form.watch('rolloutPercentage')}%</span>
                </div>
                <Slider
                  value={[form.watch('rolloutPercentage') || 0]}
                  max={100}
                  step={1}
                  onValueChange={(val) => form.setValue('rolloutPercentage', val[0])}
                />
              </div>
            )}
          </div>

          <Separator className="bg-border/40" />

          <div className="space-y-4 px-2">
            <div className="flex items-center justify-between">
              <div>
                <h3 className="text-lg font-medium flex items-center gap-2">
                  <Settings2 className="h-5 w-5" /> Targeting Rules
                </h3>
                <p className="text-sm text-muted-foreground">Serve flag only if conditions match.</p>
              </div>
              <Button type="button" variant="outline" size="sm" onClick={addConditionOR}>
                <Plus className="mr-2 h-4 w-4" /> Add Condition (OR)
              </Button>
            </div>

            <div className="space-y-6 mt-4">
              {Object.entries(groupedRules).map(([groupIdStr, groupFields], groupIndex) => {
                const groupId = parseInt(groupIdStr);
                return (
                  <div key={groupId} className="relative">
                    {groupIndex > 0 && (
                      <div className="absolute -top-[21px] left-6 bg-background px-2 text-xs font-semibold text-muted-foreground z-10 border border-border/40 rounded">
                        OR
                      </div>
                    )}
                    <div className="px-6 pt-6 pb-6 border border-border/40 rounded-lg space-y-4 bg-muted/10 relative">
                      {groupFields.map((field, i) => (
                        <div key={field.id} className="relative">
                          {i > 0 && (
                            <div className="absolute -top-4 left-6 bg-background px-1 text-[10px] font-medium text-muted-foreground z-10">
                              AND
                            </div>
                          )}
                          <div className="flex items-start gap-3">
                            <div className="flex-1 space-y-2">
                              <Input
                                placeholder="Attribute (e.g. Email)"
                                {...form.register(`rules.${field.index}.attribute`)}
                              />
                            </div>
                            <div className="w-[180px]">
                              <Select
                                value={form.watch(`rules.${field.index}.operator`)}
                                onValueChange={(val) => form.setValue(`rules.${field.index}.operator`, val)}
                              >
                                <SelectTrigger>
                                  <SelectValue placeholder="Operator" />
                                </SelectTrigger>
                                <SelectContent>
                                  {operators.map(op => (
                                    <SelectItem key={op} value={op}>{op}</SelectItem>
                                  ))}
                                </SelectContent>
                              </Select>
                            </div>
                            <div className="flex-1 space-y-2">
                              <Input
                                placeholder="Value"
                                {...form.register(`rules.${field.index}.value`)}
                              />
                            </div>
                            <Button type="button" variant="ghost" size="icon" className="text-muted-foreground hover:text-destructive shrink-0" onClick={() => remove(field.index)}>
                              <Trash2 className="h-4 w-4" />
                            </Button>
                          </div>
                          {form.formState.errors.rules?.[field.index] && (
                             <p className="text-xs text-destructive mt-1">Please fill all fields.</p>
                          )}
                        </div>
                      ))}
                      <div className="pt-2">
                        <Button type="button" variant="ghost" size="sm" className="text-xs" onClick={() => addRuleAND(groupId)}>
                          <Plus className="mr-1 h-3 w-3" /> Add Rule (AND)
                        </Button>
                      </div>
                    </div>
                  </div>
                );
              })}
              
              {fields.length === 0 && (
                <div className="text-center p-8 border border-dashed border-border/40 rounded-lg text-muted-foreground text-sm">
                  No targeting rules defined. The flag will be served based on the rollout percentage.
                </div>
              )}
            </div>
          </div>

          <SheetFooter className="mt-8 pt-4 border-t border-border/40">
            <Button type="button" variant="outline" onClick={() => onOpenChange(false)}>Cancel</Button>
            <Button type="submit" disabled={updateFlag.isPending}>
              {updateFlag.isPending ? 'Saving...' : 'Save Changes'}
            </Button>
          </SheetFooter>
        </form>
      </SheetContent>
    </Sheet>
  );
}