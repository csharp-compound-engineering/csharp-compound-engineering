# compound-docs Helm Chart

Deploys the CompoundDocs MCP Server — a GraphRAG knowledge base for C#/.NET documentation — along with optional AWS infrastructure provisioning via Crossplane/OpenTofu and secret management via the External Secrets Operator (ESO).

## Prerequisites

- Kubernetes 1.27+ (EKS recommended)
- Helm 3.x
- **Mode A/D**: Crossplane with `provider-opentofu` installed in the cluster
- **Mode A/B**: External Secrets Operator installed in the cluster
- **Mode A/B**: AWS Secrets Manager with appropriate IAM permissions
- **All modes**: Pre-existing VPC, subnets, and security groups (when using Crossplane)

### Crossplane Provider IAM Permissions

When using **Mode A** or **Mode D** (Crossplane enabled), the Crossplane `provider-opentofu` pod requires an IAM role with permissions to provision the following AWS resources:

| Service | Actions | Purpose |
|---------|---------|---------|
| Neptune (rds:*) | CreateDBSubnetGroup, CreateDBCluster, CreateDBInstance, DescribeDBClusterParameters, DescribeGlobalClusters, etc. | Provision Neptune serverless cluster |
| OpenSearch Serverless (aoss:*) | CreateCollection, CreateSecurityPolicy, CreateAccessPolicy, CreateVpcEndpoint, DeleteVpcEndpoint, UpdateVpcEndpoint, BatchGetVpcEndpoint, etc. | Provision AOSS vector search collection and VPC endpoint |
| IAM (iam:*) | CreateRole, PutRolePolicy, PassRole, CreateServiceLinkedRole, etc. | Create application Pod Identity roles and service-linked roles |
| EKS (eks:*PodIdentity*) | CreatePodIdentityAssociation, etc. | Bind IAM roles to K8s service accounts |
| Secrets Manager | CreateSecret, PutSecretValue, GetResourcePolicy, etc. | Store connection details (when ESO enabled) |
| EC2 (ec2:*) | CreateVpcEndpoint, DeleteVpcEndpoints, DescribeVpcEndpoints, ModifyVpcEndpoint, DescribeVpcs, DescribeSubnets, DescribeSecurityGroups, DescribeNetworkInterfaces, CreateTags | Create AOSS VPC endpoints for private network access |
| Route 53 (route53:*) | CreateHostedZone, DeleteHostedZone, GetChange, AssociateVPCWithHostedZone, DisassociateVPCFromHostedZone, ListHostedZonesByVPC, ListHostedZonesByName, ChangeResourceRecordSets, GetHostedZone, ListResourceRecordSets | Manage private hosted zones for AOSS VPC endpoint DNS resolution |
| STS | GetCallerIdentity | Account ID lookups in OpenTofu |

The project includes a reference OpenTofu configuration (`opentofu/`) that provisions this policy automatically in `opentofu/phases/00-prereqs`. If you are not using the reference IaC, create and attach a policy with the above permissions manually to the role assumed by the Crossplane provider pod.

## Configuration Modes

| Mode | `crossplane.enabled` | `externalSecrets.enabled` | Who creates K8s secrets? | When to use |
|------|----------------------|---------------------------|--------------------------|-------------|
| **A** | true | true | ESO (reads from AWS SM, populated by Crossplane) | Full GitOps: infra + secrets via operators |
| **B** | false | true | ESO (reads from pre-populated AWS SM) | Pre-existing infra, secrets in SM |
| **C** | false | false | Helm (static secrets from values) | Simple/dev deployments |
| **D** | true | false | Crossplane `writeConnectionSecretToRef` | Crossplane manages everything |

## Sync Wave Ordering

Resources are deployed in order via ArgoCD sync waves:

```
Wave 0: Crossplane Provider
Wave 1: Crossplane ProviderConfig
Wave 2: Crossplane Workspaces (IAM core, Neptune, OpenSearch)
Wave 3: IAM OpenSearch Policy (attaches scoped OpenSearch policy to the app role)
Wave 4: ESO resources (ServiceAccount, SecretStore, ExternalSecrets)
Wave 5: Application (Deployment, Service, ConfigMap, ServiceAccount)
```

## Deployment Ordering

> **Important — Deployment Ordering for Manual Installs:**
> The IAM OpenSearch Policy workspace (wave 3) requires the OpenSearch Serverless collection to exist before it can run. When using ArgoCD with sync waves, this ordering is handled automatically. If you are deploying manually (e.g., via `helm install` without ArgoCD), you must ensure the OpenSearch collection is fully created before the IAM OpenSearch Policy workspace reconciles. In practice this means:
>
> 1. The OpenSearch workspace (wave 2) must reach `READY=True` before the IAM OpenSearch Policy workspace (wave 3) can plan successfully.
> 2. If deploying without ArgoCD sync wave enforcement, apply the workspaces in two steps: first apply the wave 2 workspaces (IAM core, Neptune, OpenSearch), wait for all to become Ready, then apply the wave 3 workspace (IAM OpenSearch Policy).
> 3. If the IAM OpenSearch Policy workspace fails with `empty result` for the OpenSearch collection data source, it means the collection hasn't been created yet — wait for the OpenSearch workspace to complete and the workspace will self-heal on its next reconciliation loop.

## Architecture: Secret Flow

```
Mode A/B:
  AWS Secrets Manager          External Secrets Operator       Application
  ┌──────────────────┐        ┌─────────────────────┐        ┌───────────┐
  │ <prefix>/neptune │──────> │ ExternalSecret      │──────> │ K8s Secret│
  │ <prefix>/opensearch│────> │ (polls on interval) │──────> │ (target)  │
  │ <prefix>/iam     │──────> │                     │──────> │           │
  └──────────────────┘        └─────────────────────┘        └───────────┘
        ^                           |
        |                     SecretStore (Pod Identity auth)
   Crossplane TF                    |
   (Mode A only)              ESO ServiceAccount
                              (Pod Identity)

Mode C:
  Helm values ──> K8s Secret (static, from values.yaml)

Mode D:
  Crossplane Workspace ──> writeConnectionSecretToRef ──> K8s Secret
```

## AWS Secrets Manager Convention

Each service stores a single JSON secret:

| Path | JSON Structure |
|------|---------------|
| `<prefix>/neptune` | `{"endpoint": "...", "reader_endpoint": "...", "port": "8182"}` |
| `<prefix>/opensearch` | `{"endpoint": "https://...", "collection_id": "..."}` |
| `<prefix>/iam` | `{"role_arn": "arn:aws:iam::...:role/..."}` |

For **Mode B**, you must pre-populate these secrets in AWS Secrets Manager before deploying.

## Quick Start

### Mode A: Crossplane + ESO

```bash
helm install compound-docs charts/compound-docs \
  --set crossplane.enabled=true \
  --set externalSecrets.enabled=true \
  --set externalSecrets.secretsManagerPrefix=compound-docs \
  --set aws.accountId=123456789012 \
  --set vpc.vpcId=vpc-xxx \
  --set vpc.privateSubnetIds="subnet-a,subnet-b" \
  --set vpc.neptuneSecurityGroupId=sg-xxx \
  --set vpc.opensearchSecurityGroupId=sg-yyy \
  --set iam.clusterName=my-eks-cluster
```

### Mode B: ESO Only

```bash
helm install compound-docs charts/compound-docs \
  --set crossplane.enabled=false \
  --set externalSecrets.enabled=true \
  --set externalSecrets.secretsManagerPrefix=compound-docs \
  --set externalSecrets.esoRoleArn=arn:aws:iam::123456789012:role/my-eso-reader
```

### Mode C: Static Fallback

```bash
helm install compound-docs charts/compound-docs \
  --set crossplane.enabled=false \
  --set externalSecrets.enabled=false \
  --set neptune.endpoint=neptune.example.com \
  --set opensearch.endpoint=https://opensearch.example.com \
  --set iam.roleArn=arn:aws:iam::123456789012:role/my-app-role
```

### Mode D: Crossplane without ESO

```bash
helm install compound-docs charts/compound-docs \
  --set crossplane.enabled=true \
  --set externalSecrets.enabled=false \
  --set vpc.vpcId=vpc-xxx \
  --set vpc.privateSubnetIds="subnet-a,subnet-b" \
  --set vpc.neptuneSecurityGroupId=sg-xxx \
  --set vpc.opensearchSecurityGroupId=sg-yyy \
  --set iam.clusterName=my-eks-cluster
```

## Values Reference

### Global

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `image.repository` | string | `ghcr.io/compound-docs/...` | Container image repository |
| `image.tag` | string | `""` | Image tag (defaults to Chart.AppVersion) |
| `image.pullPolicy` | string | `IfNotPresent` | Image pull policy |
| `replicaCount` | int | `1` | Number of replicas |
| `nameOverride` | string | `""` | Override chart name |
| `fullnameOverride` | string | `""` | Override fully qualified app name |

### Sync Waves

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `syncWaves.provider` | string | `"0"` | Crossplane Provider wave |
| `syncWaves.providerConfig` | string | `"1"` | Crossplane ProviderConfig wave |
| `syncWaves.workspaces` | string | `"2"` | Crossplane Workspaces wave |
| `syncWaves.iamOpenSearchPolicy` | string | `"3"` | IAM OpenSearch Policy workspace wave |
| `syncWaves.externalSecrets` | string | `"4"` | ESO resources wave |
| `syncWaves.application` | string | `"5"` | Application resources wave |

### Crossplane

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `crossplane.enabled` | bool | `true` | Enable Crossplane infrastructure provisioning |
| `crossplane.provider.package` | string | `xpkg.upbound.io/...` | OpenTofu provider package |

### External Secrets

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `externalSecrets.enabled` | bool | `false` | Enable ESO integration |
| `externalSecrets.secretsManagerPrefix` | string | `""` | AWS Secrets Manager path prefix |
| `externalSecrets.refreshInterval` | string | `1h` | ESO polling interval |
| `externalSecrets.esoRoleArn` | string | `""` | Explicit ESO IAM role ARN (auto-constructed if empty) |
| `externalSecrets.esoServiceAccountName` | string | `""` | ESO ServiceAccount name (defaults to `<fullname>-eso`) |

### AWS

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `aws.region` | string | `us-east-1` | AWS region |
| `aws.accountId` | string | `""` | AWS account ID (required for deterministic role ARN construction) |
| `aws.credentialsSecretName` | string | `aws-credentials` | K8s secret with AWS credentials |
| `aws.credentialsSecretKey` | string | `credentials` | Key within the credentials secret |

### Neptune

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `neptune.enabled` | bool | `true` | Enable Neptune provisioning |
| `neptune.createServiceLinkedRole` | bool | `true` | Create the `AWSServiceRoleForNeptune` service-linked role |
| `neptune.engineVersion` | string | `"1.2.0.1"` | Neptune engine version |
| `neptune.minCapacity` | float | `2.5` | Serverless min capacity |
| `neptune.maxCapacity` | int | `128` | Serverless max capacity |
| `neptune.connectionSecretName` | string | `compound-docs-neptune` | K8s secret name for Neptune connection |
| `neptune.endpoint` | string | `""` | Direct endpoint (Mode C) |
| `neptune.port` | string | `"8182"` | Neptune port (Mode C) |

### OpenSearch

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `opensearch.enabled` | bool | `true` | Enable OpenSearch provisioning |
| `opensearch.collectionName` | string | `compound-docs-vectors` | OpenSearch Serverless collection name |
| `opensearch.connectionSecretName` | string | `compound-docs-opensearch` | K8s secret name for OpenSearch connection |
| `opensearch.endpoint` | string | `""` | Direct endpoint (Mode C) |

### IAM

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `iam.enabled` | bool | `true` | Enable IAM role provisioning |
| `iam.clusterName` | string | `""` | EKS cluster name (for Pod Identity association) |
| `iam.connectionSecretName` | string | `compound-docs-iam` | K8s secret name for IAM connection |
| `iam.roleArn` | string | `""` | Direct IAM role ARN (Mode C) |

### Application

| Key | Type | Default | Description |
|-----|------|---------|-------------|
| `serviceAccount.create` | bool | `true` | Create a ServiceAccount |
| `serviceAccount.name` | string | `""` | SA name (defaults to fullname) |
| `serviceAccount.annotations` | object | `{}` | Additional SA annotations |
| `service.type` | string | `ClusterIP` | Service type |
| `service.port` | int | `80` | Service port |
| `service.targetPort` | int | `8080` | Container port |
| `resources.limits.cpu` | string | `"1"` | CPU limit |
| `resources.limits.memory` | string | `512Mi` | Memory limit |
| `resources.requests.cpu` | string | `250m` | CPU request |
| `resources.requests.memory` | string | `256Mi` | Memory request |
| `auth.apiKeysSecretName` | string | `""` | Secret containing API keys |
| `logging.level` | string | `Information` | Application log level |

## Troubleshooting

### Check ESO sync status

```bash
# List all ExternalSecrets and their sync status
kubectl get externalsecrets -n <namespace>

# Describe a specific ExternalSecret for detailed status
kubectl describe externalsecret <release>-neptune -n <namespace>

# Check SecretStore connectivity
kubectl describe secretstore <release> -n <namespace>
```

### Common issues

- **SecretStore `InvalidProvider`**: Ensure the ESO ServiceAccount has a Pod Identity association and the IAM role has `secretsmanager:GetSecretValue` permissions.
- **ExternalSecret `SecretSyncedError`**: Verify the Secrets Manager path exists and matches the `secretsManagerPrefix` value (e.g. `compound-docs/neptune`).
- **Crossplane Workspace `AccessDenied`**: The Crossplane provider IAM role is missing permissions. If using the reference OpenTofu configuration (`opentofu/phases/00-prereqs`), re-apply after updating. Otherwise ensure the role has Neptune, OpenSearch Serverless, IAM, EKS, Secrets Manager, and STS permissions as documented in the Prerequisites section.
- **Neptune `InvalidParameterCombination` / service-linked role missing**: Neptune requires the `AWSServiceRoleForNeptune` service-linked role. The chart creates it by default (`neptune.createServiceLinkedRole: true`). If your account already has this role, set `neptune.createServiceLinkedRole: false` to skip creation.
- **IAM OpenSearch Policy workspace `empty result`**: The OpenSearch Serverless collection doesn't exist yet. Wait for the OpenSearch workspace (wave 2) to reach `READY=True` — the policy workspace will self-heal on its next reconciliation loop.
- **Crossplane provider pod identity not working after association**: After the IAM workspace creates the `eks:PodIdentityAssociation`, the Crossplane provider pod may need a restart to pick up the new credentials. Delete the provider pod to trigger a restart: `kubectl delete pod -n crossplane-system -l pkg.crossplane.io/revision`.
- **OpenSearch `ValidationException: Policy json is invalid`**: The network security policy requires either `AllowFromPublic: true` or at least one VPC endpoint. Ensure `vpc.vpcId`, `vpc.privateSubnetIds`, and `vpc.opensearchSecurityGroupId` are set correctly. The chart creates an AOSS VPC endpoint automatically.
- **Crossplane Workspace stuck**: Check `kubectl describe workspace <name>` for TF apply errors. Common cause: missing VPC/subnet/SG IDs.
- **`writeConnectionSecretToRef` conflict**: If switching from Mode D to Mode A, delete the existing K8s secrets first — Crossplane and ESO cannot both own the same secret.
