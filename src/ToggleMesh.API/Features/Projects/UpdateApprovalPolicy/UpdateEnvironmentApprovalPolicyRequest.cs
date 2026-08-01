namespace ToggleMesh.API.Features.Projects.UpdateApprovalPolicy;

public record UpdateEnvironmentApprovalPolicyRequest(
    bool RequireApprovals,
    int RequiredApprovalsCount,
    bool RequireForProtectedFlagsOnly
);
