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
    UserProfile,
    PendingInvitationDto,
    ProjectExperimentSummaryDto,
    ExperimentResultDto,
    ProjectDashboardDto,
    TimeSeriesResponsePoint,
    ExperimentIterationDto,
    SegmentDto,
    CreateSegmentRequest,
    UpdateSegmentRequest,
    UpdateFlagRequest,
    UpdateGlobalFlagSettingsRequest,
    ChangePasswordRequest,
    Integration
} from './types';

export const useSystemConfig = () => {
    return useQuery({
        queryKey: ['system', 'config'],
        queryFn: async () => {
            const { data } = await api.get<{ allowOpenRegistration: boolean, allowUserOrganizationCreation: boolean, passwordPolicy: { minimumLength: number, requireDigit: boolean, requireLowercase: boolean, requireUppercase: boolean, requireNonAlphanumeric: boolean } }>('/system/config');
            return data;
        }
    });
};

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
        mutationFn: async (req: { organizationId: string; name: string; requireTwoFactor?: boolean }) => {
            await api.put(`/organizations/${req.organizationId}`, { name: req.name, requireTwoFactor: req.requireTwoFactor });
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
            await api.post(`/organizations/${req.organizationId}/members/invite`, req);
        },
        onSuccess: (_, variables) => {
            queryClient.invalidateQueries({ queryKey: ['organizations', variables.organizationId, 'members'] });
            queryClient.invalidateQueries({ queryKey: ['organizations', variables.organizationId, 'invitations'] });
        },
    });
};

export const useOrganizationInvitations = (organizationId: string | null) => {
    return useQuery({
        queryKey: ['organizations', organizationId, 'invitations'],
        queryFn: async () => {
            if (!organizationId) return [];
            const { data } = await api.get<PendingInvitationDto[]>(`/organizations/${organizationId}/invites`);
            return data;
        },
        enabled: !!organizationId,
        placeholderData: keepPreviousData,
    });
};

export const useRevokeOrganizationInvitation = () => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (req: { organizationId: string; inviteId: string }) => {
            await api.delete(`/organizations/${req.organizationId}/invites/${req.inviteId}`);
        },
        onSuccess: (_, variables) => {
            queryClient.invalidateQueries({ queryKey: ['organizations', variables.organizationId, 'invitations'] });
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
        refetchInterval: (query) => query.state.data?.isMabEnabled ? 2000 : false,
    });
};

export const useFlagStats = (projectId: string, flagKey: string) => {
    return useQuery({
        queryKey: ['projects', projectId, 'flags', flagKey, 'stats'],
        queryFn: async () => {
            const { data } = await api.get<{ environmentId: string; variationsCount: Record<string, number> }[]>(`/projects/${projectId}/flags/${flagKey}/stats`);
            return data;
        },
        enabled: !!projectId && !!flagKey,
        refetchInterval: 10000,
    });
};

export const useCreateFeatureFlag = (projectId: string) => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (req: { key: string, type?: number, variations?: { id: string, value: string }[] }) => {
            const { data } = await api.post<{ id: string }>(`/projects/${projectId}/flags`, req);
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

export const useUpdateGlobalFlagSettings = (projectId: string, flagKey: string) => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (request: UpdateGlobalFlagSettingsRequest) => {
            await api.put(`/projects/${projectId}/flags/${flagKey}/settings`, request);
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'flags'] });
            queryClient.invalidateQueries({ queryKey: ['environments'] });
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

export const useChangePassword = () => {
    return useMutation<void, Error, ChangePasswordRequest>({
        mutationFn: async (data) => {
            await api.post('/auth/change-password', data);
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

export const useProjectExperiments = (projectId: string) => {
    return useQuery({
        queryKey: ['projects', projectId, 'experiments'],
        queryFn: async () => {
            const { data } = await api.get<ProjectExperimentSummaryDto[]>(`/projects/${projectId}/experiments?isActiveOnly=true`);
            return data;
        },
        enabled: !!projectId,
    });
};

export const useProjectHistoricalExperiments = (projectId: string) => {
    return useQuery({
        queryKey: ['projects', projectId, 'experiments', 'historical'],
        queryFn: async () => {
            const { data } = await api.get<import('./types').ProjectHistoricalExperimentDto[]>(`/projects/${projectId}/experiments/historical`);
            return data;
        },
        enabled: !!projectId,
    });
};

export const useAnalyticsSchema = (projectId: string, environmentId: string, flagKey?: string, eventName?: string) => {
    return useQuery({
        queryKey: ['analytics', 'schema', projectId, environmentId, flagKey, eventName],
        queryFn: async () => {
            const params = new URLSearchParams();
            if (flagKey) params.append('flagKey', flagKey);
            if (eventName) params.append('eventName', eventName);

            const { data } = await api.get<{ hasValue: boolean; contextKeys: string[] }>(`/projects/${projectId}/environments/${environmentId}/analytics/schema?${params.toString()}`);
            return data;
        },
        enabled: !!projectId && !!environmentId && (!!flagKey || !!eventName),
    });
};

export const useExperimentDetails = (projectId: string, environmentId: string, flagKey: string) => {
    return useQuery({
        queryKey: ['projects', projectId, 'environments', environmentId, 'flags', flagKey, 'experiments'],
        queryFn: async () => {
            const { data } = await api.get<ExperimentResultDto[]>(`/projects/${projectId}/environments/${environmentId}/flags/${flagKey}/experiments`);
            return data;
        },
        enabled: !!projectId && !!environmentId && !!flagKey,
        refetchInterval: 3000,
    });
};

export const useContextualExperimentDetails = (projectId: string, environmentId: string, flagKey: string) => {
    return useQuery({
        queryKey: ['projects', projectId, 'environments', environmentId, 'flags', flagKey, 'experiments', 'contextual'],
        queryFn: async () => {
            const { data } = await api.get<any[]>(`/projects/${projectId}/environments/${environmentId}/flags/${flagKey}/experiments/contextual`);
            return data;
        },
        enabled: !!projectId && !!environmentId && !!flagKey,
        refetchInterval: 3000,
    });
};

export const useSetContextualRollout = (projectId: string, environmentId: string, flagKey: string, onMutate?: () => void) => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (req: { contextSlice: string, rollout: { variationId: string, weight: number }[] }) => {
            await api.post(`/projects/${projectId}/environments/${environmentId}/flags/${flagKey}/contextual-rollouts/set`, req);
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'environments', environmentId, 'flags', flagKey, 'experiments', 'contextual'] });
            queryClient.invalidateQueries({ queryKey: ['flagState', environmentId, flagKey] });
            if (onMutate) onMutate();
        },
    });
};

export const useDeleteContextualRollout = (projectId: string, environmentId: string, flagKey: string) => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (contextSlice: string) => {
            await api.delete(`/projects/${projectId}/environments/${environmentId}/flags/${flagKey}/contextual-rollouts`, { data: { contextSlice } });
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'environments', environmentId, 'flags', flagKey, 'experiments', 'contextual'] });
            queryClient.invalidateQueries({ queryKey: ['environments', environmentId, 'flags', flagKey] });
        },
    });
};

export const useProjectDashboard = (projectId: string, environmentId?: string) => {
    return useQuery({
        queryKey: ['projects', projectId, 'dashboard', environmentId],
        queryFn: async () => {
            const params = new URLSearchParams();
            if (environmentId) {
                params.append('environmentId', environmentId);
            }
            const { data } = await api.get<ProjectDashboardDto>(`/projects/${projectId}/dashboard?${params.toString()}`);
            return data;
        },
        enabled: !!projectId,
        staleTime: 5000,
        refetchInterval: 30000,
        placeholderData: keepPreviousData,
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

export const useExperimentTimeSeries = (projectId: string, environmentId: string, flagKey: string, eventName: string, hours: number = 24) => {
    return useQuery({
        queryKey: ['projects', projectId, 'environments', environmentId, 'flags', flagKey, 'experiments', eventName, 'timeseries', hours],
        queryFn: async () => {
            const { data } = await api.get<TimeSeriesResponsePoint[]>(`/projects/${projectId}/environments/${environmentId}/flags/${flagKey}/experiments/${eventName}/timeseries?hours=${hours}`);
            return data;
        },
        enabled: !!projectId && !!environmentId && !!flagKey && !!eventName,
        refetchInterval: 3000,
    });
};

export const useUniqueEvents = (projectId: string, environmentId: string) => {
    return useQuery({
        queryKey: ['projects', projectId, 'environments', environmentId, 'analytics', 'events'],
        queryFn: async () => {
            const { data } = await api.get<string[]>(`/projects/${projectId}/environments/${environmentId}/analytics/events`);
            return data;
        },
        enabled: !!projectId && !!environmentId,
    });
};

export const useStartExperiment = (projectId: string, envId: string, flagKey: string) => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (req: { mode: string; goalEvent: string; optimizationType: number; contextPartitionKeys: string[], initialRolloutPercentage?: number, mabExplorationFloor?: number, balanceWeights?: boolean }) => {
            const { data } = await api.post(`/projects/${projectId}/environments/${envId}/flags/${flagKey}/experiments/start`, req);
            return data;
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['environments', envId, 'flags', flagKey] });
            queryClient.invalidateQueries({ queryKey: ['projects'] });
            queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'experiments'] });
            queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'environments', envId, 'flags', flagKey, 'experiments'] });
        },
    });
};

export const useStopExperiment = (projectId: string, envId: string, flagKey: string) => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async () => {
            const { data } = await api.post(`/projects/${projectId}/environments/${envId}/flags/${flagKey}/experiments/stop`, {});
            return data;
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['environments', envId, 'flags', flagKey] });
            queryClient.invalidateQueries({ queryKey: ['projects'] });
            queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'experiments'] });
            queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'environments', envId, 'flags', flagKey, 'experiments'] });
            queryClient.invalidateQueries({ queryKey: ['experiments', projectId, envId, flagKey, 'iterations'] });
        },
    });
};

export const useExperimentIterations = (projectId: string, envId: string, flagKey: string) => {
    return useQuery({
        queryKey: ['experiments', projectId, envId, flagKey, 'iterations'],
        queryFn: async () => {
            const { data } = await api.get<ExperimentIterationDto[]>(`/projects/${projectId}/environments/${envId}/flags/${flagKey}/experiments/iterations`);
            return data;
        },
        enabled: !!projectId && !!envId && !!flagKey,
    });
};

export const useDeleteExperimentIteration = (projectId: string, envId: string, flagKey: string) => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (iterationId: string) => {
            await api.delete(`/projects/${projectId}/environments/${envId}/flags/${flagKey}/experiments/iterations/${iterationId}`);
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['experiments', projectId, envId, flagKey, 'iterations'] });
        },
    });
};

export const useRestoreExperimentSnapshot = (projectId: string, envId: string, flagKey: string) => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (iterationId: string) => {
            const { data } = await api.post(`/projects/${projectId}/environments/${envId}/flags/${flagKey}/experiments/iterations/${iterationId}/restore`);
            return data;
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['flags', projectId, envId, flagKey] });
            queryClient.invalidateQueries({ queryKey: ['experiments', projectId, envId, flagKey, 'iterations'] });
        }
    });
};

export const useSegments = (projectId: string, environmentId: string) => {
    return useQuery({
        queryKey: ['projects', projectId, 'environments', environmentId, 'segments'],
        queryFn: async () => {
            if (!projectId || !environmentId) return [];
            const { data } = await api.get<SegmentDto[]>(`/projects/${projectId}/environments/${environmentId}/segments`);
            return data;
        },
        enabled: !!projectId && !!environmentId,
    });
};

export const useCreateSegment = () => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (req: { projectId: string; environmentId: string; data: CreateSegmentRequest }) => {
            const { data } = await api.post<SegmentDto>(`/projects/${req.projectId}/environments/${req.environmentId}/segments`, req.data);
            return data;
        },
        onSuccess: (_, variables) => {
            queryClient.invalidateQueries({ queryKey: ['projects', variables.projectId, 'environments', variables.environmentId, 'segments'] });
        },
    });
};

export const useUpdateSegment = () => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (req: { projectId: string; environmentId: string; segmentId: string; data: UpdateSegmentRequest }) => {
            const { data } = await api.put<SegmentDto>(`/projects/${req.projectId}/environments/${req.environmentId}/segments/${req.segmentId}`, req.data);
            return data;
        },
        onSuccess: (_, variables) => {
            queryClient.invalidateQueries({ queryKey: ['projects', variables.projectId, 'environments', variables.environmentId, 'segments'] });
        },
    });
};

export const useDeleteSegment = () => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (req: { projectId: string; environmentId: string; segmentId: string }) => {
            await api.delete(`/projects/${req.projectId}/environments/${req.environmentId}/segments/${req.segmentId}`);
        },
        onSuccess: (_, variables) => {
            queryClient.invalidateQueries({ queryKey: ['projects', variables.projectId, 'environments', variables.environmentId, 'segments'] });
        },
    });
};

export const useProjectIntegrations = (projectId: string) => {
    return useQuery({
        queryKey: ['projects', projectId, 'integrations'],
        queryFn: async () => {
            const { data } = await api.get<Integration[]>(`/projects/${projectId}/integrations`);
            return data;
        },
        enabled: !!projectId,
        placeholderData: keepPreviousData,
    });
};

export const useCreateIntegration = (projectId: string) => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (req: Partial<Integration>) => {
            const { data } = await api.post<Integration>(`/projects/${projectId}/integrations`, req);
            return data;
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'integrations'] });
        },
    });
};

export const useUpdateIntegration = (projectId: string) => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (req: { id: string } & Partial<Integration>) => {
            const { data } = await api.put<Integration>(`/projects/${projectId}/integrations/${req.id}`, req);
            return data;
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'integrations'] });
        },
    });
};

export const useDeleteIntegration = (projectId: string) => {
    const queryClient = useQueryClient();
    return useMutation({
        mutationFn: async (id: string) => {
            await api.delete(`/projects/${projectId}/integrations/${id}`);
        },
        onSuccess: () => {
            queryClient.invalidateQueries({ queryKey: ['projects', projectId, 'integrations'] });
        },
    });
};

export const useTestIntegration = (projectId: string) => {
    return useMutation({
        mutationFn: async (id: string) => {
            await api.post(`/projects/${projectId}/integrations/${id}/test`, {});
        }
    });
};