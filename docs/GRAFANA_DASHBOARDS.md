# Grafana Observability Dashboards

Recommended Grafana dashboards for the Road Gritting Predictor Azure deployment.

Azure Managed Grafana is used in this project, with data sources connected to Azure Monitor and Application Insights for the relevant resources.

**Azure Architecture:** API Management → Azure Container Apps → App Insights

---

## Dashboard 0: Logic App Health — ✅ Implemented

| Visualization | Type | Data Source | Status |
|---|---|---|---|
| Get Prediction API Runs | Time series | Azure Monitor — `WorkflowRunsCompleted` + `WorkflowRunsFailureRate` filtered by `GetPredictionApi` | ✅ |
| Workflow Actions | Time series | Azure Monitor — `WorkflowActionsCompleted` / `Failed` / `Skipped` filtered by `GetPredictionApi` | ✅ |
| Trigger Activity | Time series | Azure Monitor — `WorkflowTriggersCompleted` / `Failed` / `Skipped` filtered by `GetPredictionApi` | ✅ |
| HTTP Errors (4xx / 5xx) | Time series | Azure Monitor — `Http5xx` + `Http4xx` metrics | ✅ |
| Response Time | Time series | Azure Monitor — `ResponseTime` avg, thresholds at 2s / 5s | ✅ |
| Exceptions | Table | App Insights KQL — `exceptions` by type, problemId, outerMessage | ✅ |
| Error & Critical Traces | Table | App Insights KQL — `traces` with severityLevel ≥ 3 | ✅ |

---

## Dashboard 1: API Gateway Overview (API Management) — ✅ Implemented

| Visualization | Type | Data Source | Status |
|---|---|---|---|
| Total Requests | Time series | Azure Monitor — `Requests` metric | ✅ |
| Failed Requests | Time series | Azure Monitor — `FailedRequests` metric | ✅ |
| Response Codes Breakdown | Time series | Azure Monitor — `Requests` split by `GatewayResponseCodeCategory` | ✅ |
| Gateway Capacity | Time series | Azure Monitor — `Capacity` avg (0-100%) | ✅ |
| Overall Request Duration | Time series | Azure Monitor — `Duration` avg (ms) | ✅ |
| Backend Duration | Time series | Azure Monitor — `BackendDuration` avg (ms) | ✅ |
| Request Latency Percentiles (P50/P95/P99) | Time series | App Insights KQL — `requests` table | ✅ |
| Top Errors | Table | App Insights KQL — failed `requests` by resultCode + name | ✅ |

---

## Dashboard 2: Container Apps Health — ✅ Implemented

| Visualization | Type | Data Source | Status |
|---|---|---|---|
| CPU Usage (%) | Time series | Azure Monitor — `UsageNanoCores` metric | ✅ |
| Memory Usage (%) | Time series | Azure Monitor — `UsageBytes` metric | ✅ |
| Replica Count | Time series | Azure Monitor — `Replicas` metric | ✅ |
| Network In (bytes) | Time series | Azure Monitor — `RxBytes` metric | ✅ |
| Network Out (bytes) | Time series | Azure Monitor — `TxBytes` metric | ✅ |
| Container Restarts | Time series | Azure Monitor — `RestartCount` metric | ✅ |
| Request Duration by Endpoint | Time series | App Insights KQL — `requests` percentiles by name | ✅ |
| Dependency Call Failures | Table | App Insights KQL — failed `dependencies` | ✅ |

---

## Dashboard 3: ML Prediction Service — 🔲 Not Implemented

| Visualization | Type | Data Source |
|---|---|---|
| Predictions per Minute | Time series | App Insights (custom metrics/traces) |
| Gritting Decision Distribution | Pie chart | App Insights (custom events) |
| Average Salt Amount (kg) | Stat + time series | App Insights (custom metrics) |
| Prediction Confidence | Histogram | App Insights (custom metrics) |
| Ice Risk / Snow Risk Breakdown | Stacked bar | App Insights (custom events) |
| Top Requested Routes | Bar chart | App Insights |
| Model Load Status | Status panel | App Insights (`/health` traces) |
| Prediction Latency (ML inference) | Time series | App Insights (dependency tracking) |

**Prerequisites:** Custom telemetry must be emitted from the APIs for `prediction_confidence`, `salt_amount_kg`, `gritting_decision`, `ice_risk`, `snow_risk`.

---

## Dashboard 4: External Dependencies — ✅ Implemented

| Visualization | Type | Data Source | Status |
|---|---|---|---|
| Open-Meteo API Latency (P50/P95/P99) | Time series | App Insights KQL — `dependencies` table filtered by `open-meteo` | ✅ |
| Open-Meteo Success/Failure Rate | Time series | App Insights KQL — `dependencies` table success/failure counts | ✅ |
| Upstream Dependency Errors | Table | App Insights KQL — failed `dependencies` by type, target, name, resultCode | ✅ |

---

## Dashboard 5: End-to-End Request Flow — 🔲 Not Implemented

| Visualization | Type | Data Source |
|---|---|---|
| Request Volume by Client (Web UI vs API) | Stacked time series | App Insights |
| End-to-End Transaction Duration | Time series | App Insights (E2E transaction) |
| Failed Transactions | Table with drilldown | App Insights |
| Availability (Uptime %) | Stat panel | App Insights (availability tests) |

---

## Dashboard 6: Alerts & SLO Tracking — 🔲 Not Implemented

| Visualization | Type | Data Source |
|---|---|---|
| Error Budget Remaining | Gauge | Calculated (App Insights) |
| P95 Latency vs SLO Target | Time series with threshold | App Insights |
| Active Alerts | Alert list | Grafana alerting |
| Incident Timeline | Annotations overlay | Grafana annotations |

---

## Importing the Dashboard

The dashboard JSON ([grafana.json](grafana.json)) has been sanitized — all Azure subscription IDs, resource group names, and resource names are replaced with placeholders. Before importing into Grafana, find and replace the following placeholders with your actual Azure resource values:

| Placeholder | Description | Example |
|---|---|---|
| `<SUBSCRIPTION_ID_1>` | Azure subscription for Integration resources (APIM, Logic App, App Insights) | `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx` |
| `<SUBSCRIPTION_ID_2>` | Azure subscription for Container Apps resources | `xxxxxxxx-xxxx-xxxx-xxxx-xxxxxxxxxxxx` |
| `<INTEGRATION_WORKLOADS_RG>` | Resource group containing APIM and Logic App | `my-integration-workloads-rg` |
| `<INTEGRATION_MONITORING_RG>` | Resource group containing the Integration App Insights instance | `my-integration-monitoring-rg` |
| `<CONTAINERS_WORKLOADS_RG>` | Resource group containing the Container App | `my-containers-workloads-rg` |
| `<CONTAINERS_MONITORING_RG>` | Resource group containing the Container Apps App Insights instance | `my-containers-monitoring-rg` |
| `<APIM_RESOURCE_NAME>` | API Management instance name | `my-apim-instance` |
| `<LOGIC_APP_RESOURCE_NAME>` | Logic App (Standard) resource name | `my-logic-app` |
| `<CONTAINER_APP_RESOURCE_NAME>` | Azure Container App name | `my-container-app` |
| `<CONTAINER_APP_INSIGHTS_NAME>` | App Insights instance for the Container App | `my-container-app-insights` |
| `<APP_INSIGHTS_RESOURCE_NAME>` | App Insights instance for Integration resources | `my-integration-app-insights` |

---

*Last updated: 2026-02-10*
