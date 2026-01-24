################################################################################
# Phase 3: Platform — K8s Add-ons + ArgoCD
################################################################################

locals {
  argocd_fqdn = "${var.argocd_dns_name}.${var.internal_dns_zone}"

  argocd_values = yamlencode(merge(
    {
      controller = {
        replicas = 1
      }
      repoServer = {
        replicas = 1
      }
      applicationSet = {
        replicas = 1
      }
    },
    # ESO-managed secrets: disable chart-managed secret creation
    var.external_secrets_enabled ? {
      configs = {
        secret = {
          createSecret = false
        }
      }
    } : {},
  ))
}

################################################################################
# External Secrets Operator (ESO)
################################################################################

# IAM resources (role, policy, pod identity) are in phase 00-prereqs.

resource "helm_release" "external_secrets" {
  count = var.external_secrets_enabled ? 1 : 0

  name             = "external-secrets"
  repository       = "https://charts.external-secrets.io"
  chart            = "external-secrets"
  version          = var.external_secrets_chart_version
  namespace        = "external-secrets"
  create_namespace = true
  atomic           = true
  wait             = true
  timeout          = 300

  set {
    name  = "installCRDs"
    value = "true"
  }

}

################################################################################
# ExternalDNS
################################################################################

# IAM resources (role, policy, pod identity) are in phase 00-prereqs.

resource "helm_release" "external_dns" {
  count = var.external_dns_enabled ? 1 : 0

  name             = "external-dns"
  repository       = "https://kubernetes-sigs.github.io/external-dns/"
  chart            = "external-dns"
  version          = var.external_dns_chart_version
  namespace        = "external-dns"
  create_namespace = true
  atomic           = true
  wait             = true
  timeout          = 300

  set {
    name  = "provider.name"
    value = "aws"
  }

  set {
    name  = "sources[0]"
    value = "service"
  }

  set {
    name  = "sources[1]"
    value = "ingress"
  }

  set {
    name  = "domainFilters[0]"
    value = var.internal_dns_zone
  }

  set {
    name  = "policy"
    value = "upsert-only"
  }

  set {
    name  = "extraArgs[0]"
    value = "--aws-zone-type=private"
  }

  set {
    name  = "txtOwnerId"
    value = var.cluster_name
  }

}

################################################################################
# cert-manager — Self-Signed Internal CA
################################################################################

resource "helm_release" "cert_manager" {
  count = var.cert_manager_enabled ? 1 : 0

  name             = "cert-manager"
  repository       = "https://charts.jetstack.io"
  chart            = "cert-manager"
  version          = var.cert_manager_chart_version
  namespace        = "cert-manager"
  create_namespace = true
  atomic           = true
  wait             = true
  timeout          = 300

  set {
    name  = "crds.enabled"
    value = "true"
  }
}

# Self-Signed CA Bootstrap
#
# 1. ClusterIssuer "selfsigned-bootstrap" — temporary issuer to mint the CA cert
# 2. Certificate  "internal-ca"          — self-signed CA certificate (isCA: true)
# 3. ClusterIssuer "internal-ca-issuer"  — production issuer backed by the CA

resource "kubectl_manifest" "selfsigned_bootstrap_issuer" {
  count = var.cert_manager_enabled ? 1 : 0

  yaml_body = <<-YAML
    apiVersion: cert-manager.io/v1
    kind: ClusterIssuer
    metadata:
      name: selfsigned-bootstrap
    spec:
      selfSigned: {}
  YAML

  depends_on = [helm_release.cert_manager]
}

resource "kubectl_manifest" "internal_ca_certificate" {
  count = var.cert_manager_enabled ? 1 : 0

  yaml_body = <<-YAML
    apiVersion: cert-manager.io/v1
    kind: Certificate
    metadata:
      name: internal-ca
      namespace: cert-manager
    spec:
      isCA: true
      commonName: ${var.cluster_name}-internal-ca
      secretName: internal-ca-key-pair
      duration: "87600h"
      privateKey:
        algorithm: ECDSA
        size: 256
      issuerRef:
        name: selfsigned-bootstrap
        kind: ClusterIssuer
        group: cert-manager.io
  YAML

  depends_on = [kubectl_manifest.selfsigned_bootstrap_issuer]
}

resource "kubectl_manifest" "internal_ca_issuer" {
  count = var.cert_manager_enabled ? 1 : 0

  yaml_body = <<-YAML
    apiVersion: cert-manager.io/v1
    kind: ClusterIssuer
    metadata:
      name: internal-ca-issuer
    spec:
      ca:
        secretName: internal-ca-key-pair
  YAML

  depends_on = [kubectl_manifest.internal_ca_certificate]
}

################################################################################
# ArgoCD
################################################################################

resource "kubernetes_namespace" "argocd" {
  count = var.argocd_enabled ? 1 : 0

  metadata {
    name = "argocd"

    labels = {
      "app.kubernetes.io/managed-by" = "opentofu"
    }
  }
}

resource "helm_release" "argocd" {
  count = var.argocd_enabled ? 1 : 0

  name             = "argocd"
  repository       = "https://argoproj.github.io/argo-helm"
  chart            = "argo-cd"
  version          = var.argocd_chart_version
  namespace        = "argocd"
  create_namespace = false
  atomic           = true
  wait             = true
  timeout          = 600

  values = [local.argocd_values]

  # --- Server: NLB + ExternalDNS (always) ---

  set {
    name  = "server.service.type"
    value = "LoadBalancer"
  }

  set {
    name  = "server.service.annotations.service\\.beta\\.kubernetes\\.io/aws-load-balancer-scheme"
    value = "internal"
  }

  set {
    name  = "server.service.annotations.service\\.beta\\.kubernetes\\.io/aws-load-balancer-type"
    value = "nlb"
  }

  set {
    name  = "server.service.annotations.external-dns\\.alpha\\.kubernetes\\.io/hostname"
    value = local.argocd_fqdn
  }

  # --- TLS: --insecure when cert-manager is off, certificate when on ---

  dynamic "set" {
    for_each = var.cert_manager_enabled ? [] : ["insecure"]
    content {
      name  = "server.extraArgs[0]"
      value = "--insecure"
    }
  }

  dynamic "set" {
    for_each = var.cert_manager_enabled ? ["tls"] : []
    content {
      name  = "server.certificate.enabled"
      value = "true"
    }
  }

  dynamic "set" {
    for_each = var.cert_manager_enabled ? ["tls"] : []
    content {
      name  = "server.certificate.domain"
      value = local.argocd_fqdn
    }
  }

  dynamic "set" {
    for_each = var.cert_manager_enabled ? ["tls"] : []
    content {
      name  = "server.certificate.issuer.name"
      value = "internal-ca-issuer"
    }
  }

  dynamic "set" {
    for_each = var.cert_manager_enabled ? ["tls"] : []
    content {
      name  = "server.certificate.issuer.kind"
      value = "ClusterIssuer"
    }
  }

  dynamic "set" {
    for_each = var.cert_manager_enabled ? ["tls"] : []
    content {
      name  = "server.certificate.issuer.group"
      value = "cert-manager.io"
    }
  }

  dynamic "set" {
    for_each = var.cert_manager_enabled ? ["tls"] : []
    content {
      name  = "server.certificate.duration"
      value = "2160h"
    }
  }

  dynamic "set" {
    for_each = var.cert_manager_enabled ? ["tls"] : []
    content {
      name  = "server.certificate.renewBefore"
      value = "360h"
    }
  }

  dynamic "set" {
    for_each = var.cert_manager_enabled ? ["tls"] : []
    content {
      name  = "server.certificate.privateKey.algorithm"
      value = "RSA"
    }
  }

  dynamic "set" {
    for_each = var.cert_manager_enabled ? ["tls"] : []
    content {
      name  = "server.certificate.privateKey.size"
      value = "2048"
    }
  }

  depends_on = [
    kubernetes_namespace.argocd,
    helm_release.external_dns,
    kubectl_manifest.internal_ca_issuer,
    kubectl_manifest.argocd_external_secret,
  ]
}

################################################################################
# Crossplane
################################################################################

# IAM resources (role, pod identity, policy attachment) are in phase 00-prereqs.

resource "helm_release" "crossplane" {
  count = var.crossplane_enabled ? 1 : 0

  name             = "crossplane"
  repository       = "https://charts.crossplane.io/stable"
  chart            = "crossplane"
  version          = var.crossplane_chart_version
  namespace        = "crossplane-system"
  create_namespace = true
  atomic           = true
  wait             = true
  timeout          = 300

  set {
    name  = "replicas"
    value = var.crossplane_replicas
  }

  set {
    name  = "leaderElection"
    value = "true"
  }

  set {
    name  = "rbacManager.deploy"
    value = "true"
  }

  set {
    name  = "rbacManager.replicas"
    value = var.crossplane_replicas
  }

  set {
    name  = "rbacManager.leaderElection"
    value = "true"
  }

  set {
    name  = "metrics.enabled"
    value = "true"
  }

  set {
    name  = "resourcesCrossplane.limits.cpu"
    value = "500m"
  }

  set {
    name  = "resourcesCrossplane.limits.memory"
    value = "1024Mi"
  }

  set {
    name  = "resourcesCrossplane.requests.cpu"
    value = "100m"
  }

  set {
    name  = "resourcesCrossplane.requests.memory"
    value = "256Mi"
  }

  set {
    name  = "resourcesRBACManager.limits.cpu"
    value = "100m"
  }

  set {
    name  = "resourcesRBACManager.limits.memory"
    value = "512Mi"
  }

  set {
    name  = "resourcesRBACManager.requests.cpu"
    value = "100m"
  }

  set {
    name  = "resourcesRBACManager.requests.memory"
    value = "256Mi"
  }

  set {
    name  = "webhooks.enabled"
    value = "true"
  }
}

# DeploymentRuntimeConfig — pins a deterministic SA name for Pod Identity matching

resource "kubectl_manifest" "crossplane_aws_runtime_config" {
  count = var.crossplane_enabled ? 1 : 0

  yaml_body = <<-YAML
    apiVersion: pkg.crossplane.io/v1beta1
    kind: DeploymentRuntimeConfig
    metadata:
      name: provider-aws-pod-identity
    spec:
      deploymentTemplate:
        spec:
          selector: {}
          template:
            spec:
              containers:
                - name: package-runtime
              serviceAccountName: provider-opentofu
      serviceAccountTemplate:
        metadata:
          name: provider-opentofu
  YAML

  depends_on = [helm_release.crossplane]
}

################################################################################
# ArgoCD Secrets — ESO-Managed via AWS Secrets Manager
################################################################################

# ClusterSecretStore — shared ESO store for AWS Secrets Manager

resource "kubectl_manifest" "cluster_secret_store" {
  count = var.external_secrets_enabled ? 1 : 0

  yaml_body = <<-YAML
    apiVersion: external-secrets.io/v1beta1
    kind: ClusterSecretStore
    metadata:
      name: aws-secrets-manager
    spec:
      provider:
        aws:
          service: SecretsManager
          region: ${var.region}
  YAML

  depends_on = [helm_release.external_secrets]
}

# Secrets Manager resources (secret + secret version) are in phase 00-prereqs.

# ExternalSecret — syncs argocd-secret from AWS SM into the argocd namespace

resource "kubectl_manifest" "argocd_external_secret" {
  count = var.argocd_enabled && var.external_secrets_enabled ? 1 : 0

  yaml_body = <<-YAML
    apiVersion: external-secrets.io/v1beta1
    kind: ExternalSecret
    metadata:
      name: argocd-secret
      namespace: argocd
    spec:
      refreshInterval: "1h"
      secretStoreRef:
        name: aws-secrets-manager
        kind: ClusterSecretStore
      target:
        name: argocd-secret
        creationPolicy: Owner
        template:
          metadata:
            labels:
              app.kubernetes.io/name: argocd-secret
              app.kubernetes.io/part-of: argocd
      data:
        - secretKey: admin.password
          remoteRef:
            key: ${var.cluster_name}/argocd/argocd-secret
            property: admin.password
        - secretKey: admin.passwordMtime
          remoteRef:
            key: ${var.cluster_name}/argocd/argocd-secret
            property: admin.passwordMtime
        - secretKey: server.secretkey
          remoteRef:
            key: ${var.cluster_name}/argocd/argocd-secret
            property: server.secretkey
  YAML

  depends_on = [
    kubernetes_namespace.argocd,
    kubectl_manifest.cluster_secret_store,
  ]
}
