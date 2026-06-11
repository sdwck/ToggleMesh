export interface Project {
  id: string;
  name: string;
  environmentCount: number;
}

export const KeyType = {
  Server: 0,
  Client: 1
} as const;

export type KeyType = typeof KeyType[keyof typeof KeyType];

export interface EnvironmentKey {
  id: string;
  keyPrefix: string;
  keyType: KeyType;
  createdAt: string;
}

export interface Environment {
  id: string;
  name: string;
  keys: EnvironmentKey[];
  userRole: ProjectRole;
}

export interface ProjectDetails {
  id: string;
  name: string;
  environments: Environment[];
  userRole: ProjectRole;
}

export interface RuleDto {
  groupId: number;
  attribute: string;
  operator: string;
  value: string;
}

export interface FeatureFlag {
  id: string;
  key: string;
  isEnabled: boolean;
  rolloutPercentage: number | null;
  rules: RuleDto[];
  trueCount: number;
  falseCount: number;
}

export interface FlagEnvironmentStateDto {
  environmentId: string;
  isEnabled: boolean;
  rolloutPercentage: number | null;
  trueCount: number;
  falseCount: number;
  rulesCount: number;
}

export interface ProjectFlagDto {
  id: string;
  key: string;
  name: string | null;
  description: string | null;
  isClientSideExposed: boolean;
  createdAt: string;
  environments: FlagEnvironmentStateDto[];
}

export interface PaginatedResponse<T> {
  items: T[];
  totalCount: number;
  totalPages: number;
  hasNextPage: boolean;
  hasPreviousPage: boolean;
}

export interface AuditLog {
  id: string;
  entityName: string;
  entityId: string;
  action: string;
  timestamp: string;
  performedBy: string;
  oldValues: string | null;
  newValues: string | null;
}

export const ProjectRole = {
  Owner: 0,
  Admin: 1,
  Editor: 2,
  Viewer: 3,
  None: 4
} as const;

export type ProjectRole = typeof ProjectRole[keyof typeof ProjectRole];

export interface EnvironmentRoleDto {
  environmentId: string;
  role: ProjectRole;
}

export interface ProjectMember {
  id: string;
  userId: string;
  email: string;
  role: ProjectRole;
  environmentRoles: EnvironmentRoleDto[];
}

export interface UpdateFlagRequest {
  isEnabled: boolean;
  rolloutPercentage: number | null;
  rules: RuleDto[];
}

export interface UpdateMemberRequest {
  role: number;
  environmentRoles: EnvironmentRoleDto[] | null;
}

