import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import api from './axios';
import type { Project, ProjectDetails, FeatureFlag, AuditLog, PaginatedResponse, ProjectMember, UpdateFlagRequest, ProjectFlagDto, CreateKeyRequest, CreateKeyResponse, GetKeysResponse } from './types';

export const useProjects = () => {
  return useQuery({
    queryKey: ['projects'],
    queryFn: async () => {
      const { data } = await api.get<Project[]>('/projects');
      return data;
    },
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
  });
};

export const useCreateProject = () => {
  const queryClient = useQueryClient();
  return useMutation({
    mutationFn: async (name: string) => {
      const { data } = await api.post<{ id: string }>('/projects', { name });
      return data;
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

export const useProjectFlags = (projectId: string) => {
  return useQuery({
    queryKey: ['projects', projectId, 'flags'],
    queryFn: async () => {
      const { data } = await api.get<ProjectFlagDto[]>(`/projects/${projectId}/flags`);
      return data;
    },
    enabled: !!projectId,
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

export const useAuditLogs = (envId: string, page: number = 1, pageSize: number = 20) => {
  return useQuery({
    queryKey: ['environments', envId, 'audit', page, pageSize],
    queryFn: async () => {
      const { data } = await api.get<PaginatedResponse<AuditLog>>(`/audit-logs?EnvironmentId=${envId}&Page=${page}&PageSize=${pageSize}`);
      return data;
    },
    enabled: !!envId,
  });
};

export const useProjectAuditLogs = (projectId: string, page: number = 1, pageSize: number = 20) => {
  return useQuery({
    queryKey: ['projects', projectId, 'audit', page, pageSize],
    queryFn: async () => {
      const { data } = await api.get<PaginatedResponse<AuditLog>>(`/audit-logs?ProjectId=${projectId}&Page=${page}&PageSize=${pageSize}`);
      return data;
    },
    enabled: !!projectId,
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
    },
  });
};