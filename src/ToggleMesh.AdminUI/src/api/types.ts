export interface Project {
  id: string;
  name: string;
  environmentCount: number;
}

export interface EnvironmentKey {
  id: string;
  keyPrefix: string;
  createdAt: string;
}

export interface Environment {
  id: string;
  name: string;
  keys: EnvironmentKey[];
}

export interface ProjectDetails {
  id: string;
  name: string;
  environments: Environment[];
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
} as const;

export type ProjectRole = typeof ProjectRole[keyof typeof ProjectRole];

export interface ProjectMember {
  id: string;
  userId: string;
  email: string;
  role: ProjectRole;
}

export interface UpdateFlagRequest {
  isEnabled: boolean;
  rolloutPercentage: number | null;
  rules: RuleDto[];
}

