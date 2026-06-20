import { useQuery, useMutation, useQueryClient, keepPreviousData, useInfiniteQuery } from '@tanstack/react-query';
import api from './axios';
import type {
    Project,
    ProjectDetails,
    FeatureFlag,
    AuditLog,
    CursorPagedResponse,
    PaginatedResponse,
    ProjectMember,
    UpdateFlagRequest,
    ProjectFlagDto,
    CreateKeyRequest,
    CreateKeyResponse,
    GetKeysResponse,
    CreateWebhookRequest,
    Webhook,
    CreateTokenRequest,
    CreateTokenResponse,
    TokenDto,
    OrganizationDto,
    OrganizationMemberDto,
    UserProfile
} from './types';

export const useOrganizations = () => {
    return useQuery({
        queryKey: ['organizations'],
        queryFn: async () => {
            const { data } = await api.get<OrganizationDto[]>('/organizations');
            return data;
        },
        staleTime: 5 * 60 * 1000,
        gcTime: 10 * 60 * 1000,
    });
};

export const useCreateOrganization = () => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (req: { name: string }) => {
            const { data } = await api.post<OrganizationDto>('/organizations', req);
            return data;
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['organizations'] });
        },
    });
};

export const useUpdateOrganization = () => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (req: { organizationId: string; name: string }) => {
            await api.put(`/organizations/${req.organizationId}`, { name: req.name });
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['organizations'] });
        },
    });
};

export const useDeleteOrganization = () => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (organizationId: string) => {
            await api.delete(`/organizations/${organizationId}`);
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['organizations'] });
        },
    });
};

export const useOrganizationMembers = (organizationId: string | null) => {
    return useQuery({
        queryKey: ['organizations', organizationId, 'members'],
        queryFn: async () => {
            if (!organizationId) return [];
            const { data } = await api.get<OrganizationMemberDto[]>(`/organizations/${organizationId}/members`);
            return data;
        },
        enabled: !!organizationId,
        placeholderData: keepPreviousData,
    });
};

export const useInviteOrganizationMember = () => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (req: { organizationId: string; email: string; role: number }) => {
            const { data } = await api.post<OrganizationMemberDto>(`/organizations/${req.organizationId}/members/invite`, req);
            return data;
        },
        onSuccess: (_, variables) => {
            queryClient.invalidateQueries({ queryKey: ['organizations', variables.organizationId, 'members'] });
        },
    });
};

export const useUpdateOrganizationMember = () => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (req: { organizationId: string; userId: string; role: number }) => {
            await api.put(`/organizations/${req.organizationId}/members/${req.userId}`, { role: req.role });
        },
        onSuccess: (_, variables) => {
            queryClient.invalidateQueries({ queryKey: ['organizations', variables.organizationId, 'members'] });
        },
    });
};

export const useRemoveOrganizationMember = () => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (req: { organizationId: string; userId: string }) => {
            await api.delete(`/organizations/${req.organizationId}/members/${req.userId}`);
        },
        onSuccess: (_, variables) => {
            queryClient.invalidateQueries({ queryKey: ['organizations', variables.organizationId, 'members'] });
        },
    });
};

export const useProjects = (organizationId: string | null) => {
    return useQuery({
        queryKey: ['projects', organizationId],
        queryFn: async () => {
            const url = organizationId ? `/projects?organizationId=${organizationId}` : '/projects';
            const { data } = await api.get<{ items: Project[] }>(url);
            return data.items;
        },
        staleTime: 5 * 60 * 1000,
        gcTime: 10 * 60 * 1000,
        placeholderData: keepPreviousData,
    });
};

export const useProjectDetails = (projectId: string) => {
    return useQuery({
        queryKey: ['projects', projectId],
        queryFn: async () => {
            const { data } = await api.get<ProjectDetails>(`/projects/${projectId}`);
            return data;
        },
        enabled: !!projectId,
        placeholderData: keepPreviousData,
    });
};

export const useCreateProject = () => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (req: { name: string; organizationId: string }) => {
            const { data } = await api.post<{ id: string; name: string }>('/projects', req);
            return data;
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['projects'] });
        },
    });
};

export const useUpdateProject = (projectId: string) => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (name: string) => {
            await api.put(`/projects/${projectId}`, { name });
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['projects'] });
            queryClient.invalidateQueries({ queryKey: ['projects', projectId] });
        },
    });
};

export const useDeleteProject = () => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (projectId: string) => {
            await api.delete(`/projects/${projectId}`);
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['projects'] });
        },
    });
};

export const useCreateEnvironment = (projectId: string) => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (name: string) => {
            const { data } = await api.post<{ id: string }>(`/projects/${projectId}/environments`, { name });
            return data;
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['projects', projectId] });
            queryClient.invalidateQueries({ queryKey: ['projects'] });
        },
    });
};

export const useUpdateEnvironment = (projectId: string, envId: string) => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (name: string) => {
            await api.put(`/projects/${projectId}/environments/${envId}`, { name });
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['projects', projectId] });
            queryClient.invalidateQueries({ queryKey: ['projects'] });
        },
    });
};

export const useDeleteEnvironment = (projectId: string) => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (envId: string) => {
            await api.delete(`/projects/${projectId}/environments/${envId}`);
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['projects', projectId] });
            queryClient.invalidateQueries({ queryKey: ['projects'] });
        },
    });
};

export const useEnvironmentKeys = (projectId: string, envId: string) => {
    return useQuery({
        queryKey: ['projects', projectId, 'environments', envId, 'keys'],
        queryFn: async () => {
            const { data } = await api.get<GetKeysResponse[]>(`/projects/${projectId}/environments/${envId}/keys`);
            return data;
        },
        enabled: !!projectId && !!envId,
        placeholderData: keepPreviousData,
    });
};

export const useCreateEnvironmentKey = (projectId: string, envId: string) => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (request: CreateKeyRequest) => {
            const { data } = await api.post<CreateKeyResponse>(`/projects/${projectId}/environments/${envId}/keys`, request);
            return data;
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'environments', envId, 'keys'] });
        },
    });
};

export const useRevokeEnvironmentKey = (projectId: string, envId: string) => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (keyId: string) => {
            await api.delete(`/projects/${projectId}/environments/${envId}/keys/${keyId}`);
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'environments', envId, 'keys'] });
        },
    });
};

export const useProjectFlags = (projectId: string, search?: string, tags?: string[]) => {
    return useQuery({
        queryKey: ['projects', projectId, 'flags', search, tags, 'all'],
        queryFn: async () => {
            const params = new URLSearchParams({ PageSize: '1000' });
            if (search) params.append('Search', search);
            tags?.forEach(t => params.append('Tags', t));
            const { data } = await api.get<PaginatedResponse<ProjectFlagDto>>(`/projects/${projectId}/flags?${params.toString()}`);
            return data.items;
        },
        enabled: !!projectId,
        placeholderData: keepPreviousData,
    });
};

export const useProjectFlagsInfinite = (projectId: string, search?: string, tags?: string[], pageSize = 20) => {
    return useInfiniteQuery({
        queryKey: ['projects', projectId, 'flags', search, tags, 'infinite', pageSize],
        initialPageParam: 1 as number,
        queryFn: async ({ pageParam }: { pageParam: number }) => {
            const params = new URLSearchParams({ PageSize: pageSize.toString(), Page: pageParam.toString() });
            if (search) params.append('Search', search);
            tags?.forEach(t => params.append('Tags', t));
            const { data } = await api.get<PaginatedResponse<ProjectFlagDto>>(`/projects/${projectId}/flags?${params.toString()}`);
            return data;
        },
        getNextPageParam: (lastPage: PaginatedResponse<ProjectFlagDto>, allPages) => lastPage.hasNextPage ? allPages.length + 1 : null,
        enabled: !!projectId,
        placeholderData: keepPreviousData,
    });
};

export const useFeatureFlag = (projectId: string, envId: string, flagKey: string) => {
    return useQuery({
        queryKey: ['environments', envId, 'flags', flagKey],
        queryFn: async () => {
            const { data } = await api.get<FeatureFlag>(`/projects/${projectId}/environments/${envId}/flags/${flagKey}`);
            return data;
        },
        enabled: !!envId && !!projectId && !!flagKey,
    });
};

export const useCreateFeatureFlag = (projectId: string) => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (key: string) => {
            const { data } = await api.post<{ id: string }>(`/projects/${projectId}/flags`, { key });
            return data;
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'flags'] });
            queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'tags'] });
        },
    });
};

export const useUpdateFlagPrivacy = (projectId: string) => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async ({ flagKey, isClientSideExposed }: { flagKey: string; isClientSideExposed: boolean }) => {
            await api.patch(`/projects/${projectId}/flags/${flagKey}/privacy`, { isClientSideExposed });
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'flags'] });
        },
    });
};

export const useToggleFeatureFlag = (projectId: string) => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async ({ envId, flagKey, isEnabled }: { envId: string; flagKey: string; isEnabled: boolean }) => {
            await api.post(`/projects/${projectId}/environments/${envId}/flags/${flagKey}/toggle`, { isEnabled });
            return envId;
        },
        onSuccess: (envId) => {
            queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'flags'] });
            queryClient.invalidateQueries({ queryKey: ['environments', envId, 'flags'] });
            queryClient.invalidateQueries({ queryKey: ['environments', envId, 'audit'] });
        },
    });
};

const calculateDateFrom = (type: string): string => {
    const now = new Date();
    switch (type) {
        case '5m': now.setMinutes(now.getMinutes() - 5); return now.toISOString();
        case '15m': now.setMinutes(now.getMinutes() - 15); return now.toISOString();
        case '30m': now.setMinutes(now.getMinutes() - 30); return now.toISOString();
        case '1h': now.setHours(now.getHours() - 1); return now.toISOString();
        case '3h': now.setHours(now.getHours() - 3); return now.toISOString();
        case '6h': now.setHours(now.getHours() - 6); return now.toISOString();
        case '12h': now.setHours(now.getHours() - 12); return now.toISOString();
        case '24h': now.setHours(now.getHours() - 24); return now.toISOString();
        case '2d': now.setDate(now.getDate() - 2); return now.toISOString();
        case '7d': now.setDate(now.getDate() - 7); return now.toISOString();
        default: return '';
    }
};

export const useAuditLogs = (
    envId: string,
    pageSize = 20,
    action?: string,
    entityName?: string,
    sortOrder?: string,
    rangeType = 'all',
    customFrom?: string,
    customTo?: string,
    search?: string
) => {
    return useInfiniteQuery({
        queryKey: ['environments', envId, 'audit', pageSize, action, entityName, sortOrder, rangeType, customFrom, customTo, search],
        initialPageParam: null as string | null,
        queryFn: async ({ pageParam }: { pageParam: string | null }) => {
            const params = new URLSearchParams({
                EnvironmentId: envId,
                PageSize: pageSize.toString()
            });

            if (pageParam) params.append('Cursor', pageParam);
            if (action && action !== 'all') params.append('Action', action);
            if (entityName && entityName !== 'all') params.append('EntityName', entityName);
            if (sortOrder) params.append('SortOrder', sortOrder);
            if (search) params.append('Search', search);

            let dateFrom: string | undefined;
            let dateTo: string | undefined;

            if (rangeType === 'custom') {
                if (customFrom) dateFrom = new Date(customFrom).toISOString();
                if (customTo) dateTo = new Date(customTo).toISOString();
            } else {
                const calculated = calculateDateFrom(rangeType);
                if (calculated) dateFrom = calculated;
            }

            if (dateFrom) params.append('DateFrom', dateFrom);
            if (dateTo) params.append('DateTo', dateTo);

            const { data } = await api.get<CursorPagedResponse<AuditLog>>(`/audit-logs?${params.toString()}`);
            return data;
        },
        getNextPageParam: (lastPage: CursorPagedResponse<AuditLog>) => lastPage.hasNextPage ? lastPage.nextCursor : null,
        enabled: !!envId,
        placeholderData: keepPreviousData,
    });
};

export const useProjectAuditLogs = (
    projectId: string,
    pageSize = 20,
    action?: string,
    entityName?: string,
    sortOrder?: string,
    rangeType = 'all',
    customFrom?: string,
    customTo?: string,
    search?: string
) => {
    return useInfiniteQuery({
        queryKey: ['projects', projectId, 'audit', pageSize, action, entityName, sortOrder, rangeType, customFrom, customTo, search],
        initialPageParam: null as string | null,
        queryFn: async ({ pageParam }: { pageParam: string | null }) => {
            const params = new URLSearchParams({
                ProjectId: projectId,
                PageSize: pageSize.toString()
            });

            if (pageParam) params.append('Cursor', pageParam);
            if (action && action !== 'all') params.append('Action', action);
            if (entityName && entityName !== 'all') params.append('EntityName', entityName);
            if (sortOrder) params.append('SortOrder', sortOrder);
            if (search) params.append('Search', search);

            let dateFrom: string | undefined;
            let dateTo: string | undefined;

            if (rangeType === 'custom') {
                if (customFrom) dateFrom = new Date(customFrom).toISOString();
                if (customTo) dateTo = new Date(customTo).toISOString();
            } else {
                const calculated = calculateDateFrom(rangeType);
                if (calculated) dateFrom = calculated;
            }

            if (dateFrom) params.append('DateFrom', dateFrom);
            if (dateTo) params.append('DateTo', dateTo);

            const { data } = await api.get<CursorPagedResponse<AuditLog>>(`/audit-logs?${params.toString()}`);
            return data;
        },
        getNextPageParam: (lastPage: CursorPagedResponse<AuditLog>) => lastPage.hasNextPage ? lastPage.nextCursor : null,
        enabled: !!projectId,
        placeholderData: keepPreviousData,
    });
};

export const useUpdateFeatureFlag = (projectId: string, envId: string, flagKey: string) => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (request: UpdateFlagRequest) => {
            await api.put(`/projects/${projectId}/environments/${envId}/flags/${flagKey}`, request);
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'flags'] });
            queryClient.invalidateQueries({ queryKey: ['environments', envId, 'flags'] });
            queryClient.invalidateQueries({ queryKey: ['environments', envId, 'audit'] });
        },
    });
};

export const useUpdateProjectMember = (projectId: string) => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async ({ userId, role, environmentRoles }: { userId: string; role: number; environmentRoles: { environmentId: string; role: number }[] | null }) => {
            await api.put(`/projects/${projectId}/members/${userId}`, { role, environmentRoles });
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'members'] });
        },
    });
};

export const useRemoveProjectMember = (projectId: string) => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (userId: string) => {
            await api.delete(`/projects/${projectId}/members/${userId}`);
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'members'] });
        },
    });
};

export const useCloneEnvironment = (projectId: string) => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async ({ sourceEnvId, targetEnvId }: { sourceEnvId: string; targetEnvId: string }) => {
            await api.post(`/projects/${projectId}/environments/${sourceEnvId}/clone-to/${targetEnvId}`);
        },
        onSuccess: (_, variables) => {
            queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'flags'] });
            queryClient.invalidateQueries({ queryKey: ['environments', variables.targetEnvId] });
        },
    });
};

export const useProjectMembers = (projectId: string) => {
    return useQuery({
        queryKey: ['projects', projectId, 'members'],
        queryFn: async () => {
            const { data } = await api.get<ProjectMember[]>(`/projects/${projectId}/members`);
            return data;
        },
        enabled: !!projectId,
        placeholderData: keepPreviousData,
    });
};

export const useAddProjectMember = (projectId: string) => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (request: { email: string; role: number }) => {
            await api.post(`/projects/${projectId}/members`, request);
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'members'] });
        },
    });
};

export const useDeleteFeatureFlag = (projectId: string) => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (flagKey: string) => {
            await api.delete(`/projects/${projectId}/flags/${flagKey}`);
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'flags'] });
            queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'tags'] });
        },
    });
};

export const useProjectWebhooks = (projectId: string) => {
    return useQuery({
        queryKey: ['projects', projectId, 'webhooks'],
        queryFn: async () => {
            const { data } = await api.get<Webhook[]>(`/projects/${projectId}/webhooks`);
            return data;
        },
        enabled: !!projectId,
        placeholderData: keepPreviousData,
    });
};

export const useCreateWebhook = (projectId: string) => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (request: CreateWebhookRequest) => {
            const { data } = await api.post<Webhook>(`/projects/${projectId}/webhooks`, request);
            return data;
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'webhooks'] });
        },
    });
};

export const useDeleteWebhook = (projectId: string) => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (id: string) => {
            await api.delete(`/projects/${projectId}/webhooks/${id}`);
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'webhooks'] });
        },
    });
};

export const usePersonalTokens = () => {
    return useQuery({
        queryKey: ['user', 'tokens'],
        queryFn: async () => {
            const { data } = await api.get<TokenDto[]>('/user/tokens');
            return data;
        },
    });
};

export const useUserProfile = () => {
    return useQuery({
        queryKey: ['user', 'profile'],
        queryFn: async () => {
            const { data } = await api.get<UserProfile>('/user/profile');
            return data;
        },
        placeholderData: keepPreviousData,
    });
};

export const useUpdateUserProfile = () => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (req: { username: string }) => {
            await api.put('/user/profile', req);
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['user', 'profile'] });
        },
    });
};

export const useCreatePersonalToken = () => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (request: CreateTokenRequest) => {
            const { data } = await api.post<CreateTokenResponse>('/user/tokens', request);
            return data;
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['user', 'tokens'] });
        },
    });
};

export const useDeletePersonalToken = () => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (id: string) => {
            await api.delete(`/user/tokens/${id}`);
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['user', 'tokens'] });
        },
    });
};

export const useReorderEnvironments = (projectId: string) => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (environmentIds: string[]) => {
            await api.post(`/projects/${projectId}/environments/reorder`, { environmentIds });
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['projects', projectId] });
            queryClient.invalidateQueries({ queryKey: ['projects'] });
        },
    });
};

export const useProjectTags = (projectId: string) => {
    return useQuery({
        queryKey: ['projects', projectId, 'tags'],
        queryFn: async () => {
            const { data } = await api.get<string[]>(`/projects/${projectId}/tags`);
            return data;
        },
        enabled: !!projectId,
    });
};

export const useUpdateFlagMetadata = (projectId: string, flagKey: string) => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (request: { name: string; description: string; tags: string[] }) => {
            await api.put(`/projects/${projectId}/flags/${flagKey}/metadata`, request);
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'flags'] });
            queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'tags'] });
            queryClient.invalidateQueries({ queryKey: ['environments'] });
        },
    });
};

export const useRuleOperators = () => {
    return useQuery({
        queryKey: ['flags', 'operators'],
        queryFn: async () => {
            const { data } = await api.get<string[]>('/flags/operators');
            return data;
        },
        staleTime: Infinity,
    });
};

export const useUpdateWebhook = (projectId: string) => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async ({ webhookId, data }: { webhookId: string; data: Partial<Webhook> }) => {
            const { data: res } = await api.put(`/projects/${projectId}/webhooks/${webhookId}`, data);
            return res;
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'webhooks'] });
        },
    });
};

export const useUpdateWebhookStatus = (projectId: string) => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async ({ webhookId, status }: { webhookId: string; status: number }) => {
            const { data } = await api.put(`/projects/${projectId}/webhooks/${webhookId}/status`, { status });
            return data;
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'webhooks'] });
        },
    });
};

export const useWebhookDeliveries = (projectId: string, webhookId: string, page: number) => {
    return useQuery({
        queryKey: ['webhookDeliveries', projectId, webhookId, page],
        queryFn: async () => {
            const { data } = await api.get(`/projects/${projectId}/webhooks/${webhookId}/deliveries?page=${page}&pageSize=10`);
            return data;
        },
        enabled: !!webhookId && !!projectId,
        refetchInterval: 3000,
    });
};

export const useRetryWebhookDelivery = (projectId: string, webhookId: string) => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (deliveryId: string) => {
            await api.post(`/projects/${projectId}/webhooks/${webhookId}/deliveries/${deliveryId}/retry`);
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['webhookDeliveries'] });
        },
    });
};

export const useCancelWebhookDelivery = (projectId: string, webhookId: string) => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (deliveryId: string) => {
            await api.post(`/projects/${projectId}/webhooks/${webhookId}/deliveries/${deliveryId}/cancel`);
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['webhookDeliveries'] });
        },
    });
};