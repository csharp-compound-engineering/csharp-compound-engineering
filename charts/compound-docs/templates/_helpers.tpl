{{/*
Expand the name of the chart.
*/}}
{{- define "compound-docs.name" -}}
{{- default .Chart.Name .Values.nameOverride | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Create a default fully qualified app name.
*/}}
{{- define "compound-docs.fullname" -}}
{{- if .Values.fullnameOverride }}
{{- .Values.fullnameOverride | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- $name := default .Chart.Name .Values.nameOverride }}
{{- if contains $name .Release.Name }}
{{- .Release.Name | trunc 63 | trimSuffix "-" }}
{{- else }}
{{- printf "%s-%s" .Release.Name $name | trunc 63 | trimSuffix "-" }}
{{- end }}
{{- end }}
{{- end }}

{{/*
Create chart name and version as used by the chart label.
*/}}
{{- define "compound-docs.chart" -}}
{{- printf "%s-%s" .Chart.Name .Chart.Version | replace "+" "_" | trunc 63 | trimSuffix "-" }}
{{- end }}

{{/*
Common labels
*/}}
{{- define "compound-docs.labels" -}}
helm.sh/chart: {{ include "compound-docs.chart" . }}
{{ include "compound-docs.selectorLabels" . }}
{{- if .Chart.AppVersion }}
app.kubernetes.io/version: {{ .Chart.AppVersion | quote }}
{{- end }}
app.kubernetes.io/managed-by: {{ .Release.Service }}
{{- end }}

{{/*
Selector labels
*/}}
{{- define "compound-docs.selectorLabels" -}}
app.kubernetes.io/name: {{ include "compound-docs.name" . }}
app.kubernetes.io/instance: {{ .Release.Name }}
{{- end }}

{{/*
Create the name of the service account to use
*/}}
{{- define "compound-docs.serviceAccountName" -}}
{{- if .Values.serviceAccount.create }}
{{- default (include "compound-docs.fullname" .) .Values.serviceAccount.name }}
{{- else }}
{{- default "default" .Values.serviceAccount.name }}
{{- end }}
{{- end }}

{{/*
ESO ServiceAccount name
*/}}
{{- define "compound-docs.esoServiceAccountName" -}}
{{- default (printf "%s-eso" (include "compound-docs.fullname" .)) .Values.externalSecrets.esoServiceAccountName }}
{{- end }}

{{/*
ESO reader IAM role ARN — explicit value or deterministic from accountId
*/}}
{{- define "compound-docs.esoRoleArn" -}}
{{- if .Values.externalSecrets.esoRoleArn }}
{{- .Values.externalSecrets.esoRoleArn }}
{{- else }}
{{- printf "arn:aws:iam::%s:role/%s-eso-reader" (toString .Values.aws.accountId) (include "compound-docs.fullname" .) }}
{{- end }}
{{- end }}

{{/*
Secrets Manager path for a service: <prefix>/<service>
*/}}
{{- define "compound-docs.smSecretPath" -}}
{{- printf "%s/%s" .prefix .service }}
{{- end }}

