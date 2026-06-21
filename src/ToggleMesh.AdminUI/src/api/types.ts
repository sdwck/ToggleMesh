export const OrganizationRole = {
    Member: 0,
    Admin: 1
} as const;

export type OrganizationRole = typeof OrganizationRole[keyof typeof OrganizationRole];

export interface OrganizationDto {
    id: string;
    name: string;
    createdAt: string;
    role: OrganizationRole;
}

export interface OrganizationMemberDto {
    userId: string;
    email: string;
    role: OrganizationRole;
    createdAt: string;
}

export interface OrganizationInvitationDto {
    organizationName: string;
    email: string;
    role: OrganizationRole;
}

export interface PendingInvitationDto {
    id: string;
    email: string;
    role: OrganizationRole;
    invitedAt: string;
    expiresAt: string;
    token: string;
}

export interface ProjectEnvironmentDto {
    id: string;
    name: string;
}

export interface Project {
    id: string;
    name: string;
    environmentCount: number;
    environments: ProjectEnvironmentDto[];
    userRole: ProjectRole;
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
    createdAt: string;
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
    tags: string[];
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
    updatedAt: string;
    environments: FlagEnvironmentStateDto[];
    tags: string[];
}

export interface PaginatedResponse<T> {
    items: T[];
    totalCount: number;
    totalPages: number;
    hasNextPage: boolean;
    hasPreviousPage: boolean;
}

export interface CursorPagedResponse<T> {
    items: T[];
    totalCount: number;
    nextCursor: string | null;
    hasNextPage: boolean;
}

export interface AuditLog {
    id: string;
    entityName: string;
    entityFriendlyName: string;
    entityId: string;
    action: string;
    timestamp: string;
    performedById: string | null;
    performedByEmail: string;
    performedBy: string;
    oldValues: string | null;
    newValues: string | null;
}

export interface CreateKeyRequest {
    name: string;
    type: KeyType;
}

export interface CreateKeyResponse {
    id: string;
    name: string;
    keyType: KeyType;
    keyPreview: string;
    createdOn: string;
    plainKey: string;
}

export interface GetKeysResponse {
    id: string;
    name: string;
    keyType: KeyType;
    keyPreview: string;
    createdOn: string;
    expireOn: string | null;
    lastUsedAt: string | null;
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
    isOrganizationAdmin: boolean;
    environmentRoles: EnvironmentRoleDto[];
    createdAt: string;
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

export interface Webhook {
    id: string;
    projectId: string;
    name: string;
    url: string;
    secretKey: string;
    isActive: boolean;
    environmentIds: string[];
    events: string[];
    createdAt: string;
    lastTriggeredAt: string | null;
}

export interface CreateWebhookRequest {
    name: string;
    url: string;
    environmentIds: string[];
    events: string[];
}

export interface TokenDto {
    id: string;
    name: string;
    preview: string;
    createdAt: string;
    expiresAt: string | null;
    lastUsedAt: string | null;
}

export interface CreateTokenRequest {
    name: string;
    expiresInDays: number | null;
}

export interface CreateTokenResponse {
    id: string;
    name: string;
    plainToken: string;
    createdAt: string;
    expiresAt: string | null;
}

export interface UserProfile {
    id: string;
    email: string;
    username: string;
}

export const WebhookStatus = {
    Active: 0,
    Failing: 1,
    DisabledBySystem: 2,
    Paused: 3
} as const;

export type WebhookStatus = typeof WebhookStatus[keyof typeof WebhookStatus];

export const WebhookDeliveryStatus = {
    Pending: 0,
    Success: 1,
    Failed: 2,
    Canceled: 3
} as const;

export type WebhookDeliveryStatus = typeof WebhookDeliveryStatus[keyof typeof WebhookDeliveryStatus];

export interface Webhook {
    id: string;
    projectId: string;
    name: string;
    url: string;
    secretKey: string;
    status: WebhookStatus;
    consecutiveFailures: number;
    environmentIds: string[];
    events: string[];
    lastTriggeredAt: string | null;
    createdAt: string;
}

export interface WebhookDelivery {
    id: string;
    webhookId: string;
    eventName: string;
    payload: string;
    status: WebhookDeliveryStatus;
    statusCode: number | null;
    errorMessage: string | null;
    attemptCount: number;
    nextAttemptAt: string | null;
    completedAt: string | null;
    createdAt: string;
}