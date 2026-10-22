\# Rolling Deployment Procedure

\## 1. Strategy overview

A rolling deployment replaces old API pods with new ones one at a time. Old and new versions serve traffic side by side until the rollout completes, and capacity never drops to zero.

```mermaid

flowchart LR

&#x20;   A\[Merge to main] --> B\[CI: build and test]

&#x20;   B --> C\[CD: build and push image to GHCR]

&#x20;   C --> D\[kubectl set image]

&#x20;   D --> E\[New pod starts]

&#x20;   E --> F{Readiness probe /health/ready passes?}

&#x20;   F -- yes --> G\[Pod added to Service, one old pod removed]

&#x20;   G --> H{All pods replaced?}

&#x20;   H -- no --> E

&#x20;   H -- yes --> I\[Verify]

&#x20;   F -- no --> J\[Rollout stalls, old pods keep serving]

&#x20;   J --> K\[kubectl rollout undo]

```

Traffic path: `Client → Nginx (nginx-service) → ebay-api-service (ClusterIP :80) → ebay-api pods (:8080)`. SignalR messages are shared between pods through Redis.

\## 2. Components and why they are used

| Component | File | Purpose in the rollout |

|---|---|---|

| GitHub Actions CI | `.github/workflows/ci.yml` | Restores, builds and tests so a broken build never becomes an image. |

| GitHub Actions CD | `.github/workflows/cd.yml` | Builds the image and pushes `latest` and `sha-<short>` tags to `ghcr.io/monomee/ebay\_clone\_adminrole`. |

| Dockerfile | `Dockerfile` | Three stages (React build, .NET publish, lean runtime). One identical artifact for every pod. |

| Deployment | `k8s/api-deployment.yaml` | The rolling engine: `replicas: 2`, `RollingUpdate`, `maxSurge: 1`, `maxUnavailable: 0`. |

| Readiness probe | `k8s/api-deployment.yaml` | Gate: a new pod gets no traffic until `/health/ready` connects to SQL Server. |

| Liveness probe | `k8s/api-deployment.yaml` | Restarts the container when `/health/live` stops responding. It does not check SQL Server. |

| Service | `k8s/api-service.yaml` | Routes only to ready pods, which moves traffic between versions. |

| Nginx | `k8s/nginx-config.yaml`, `k8s/nginx-deployment.yaml`, `k8s/nginx-service.yaml` | Public entry point; upgrades websockets for `/hubs/`; adds `X-Backend-Server` header to show which pod answered. |

| Redis | `k8s/redis-deployment.yaml` | SignalR backplane, so real-time messages work across old and new pods. |

| ConfigMap / Secret | `k8s/configmap.yaml`, `k8s/secret.yaml` | Configuration and credentials kept out of the image. |

| SQL Server | `k8s/db-statefulset.yaml` | Shared by old and new pods during the rollout. |

| kubectl | CLI | Applies manifests, triggers, watches and reverts rollouts. |

\## 3. Prerequisites

\- `kubectl` configured against the target cluster.

\- Permission to pull from `ghcr.io/monomee/ebay\_clone\_adminrole` (make the package public or add an image pull secret).

\- `k8s/secret.yaml` populated with real values (`SA\_PASSWORD\_ACTUAL`, `JWT\_KEY`, `EMAIL\_PASSWORD`). Do not commit real secrets.

\## 4. One-time cluster setup

Run in this order:

```powershell

kubectl apply -f k8s/namespace.yaml

kubectl apply -f k8s/configmap.yaml -f k8s/secret.yaml

kubectl apply -f k8s/db-statefulset.yaml -f k8s/redis-deployment.yaml

kubectl apply -f k8s/api-deployment.yaml -f k8s/api-service.yaml

kubectl apply -f k8s/nginx-config.yaml -f k8s/nginx-deployment.yaml -f k8s/nginx-service.yaml

```

Confirm everything is ready:

```powershell

kubectl -n ebay-clone get pods

kubectl -n ebay-clone get svc

```

\## 5. Release procedure

\### Step 1: Pre-release checks

\- \[ ] Database changes are backward-compatible (add first, remove later) because old and new pods share one database.

\- \[ ] `/health/ready` connects to SQL Server. `/health/live` only checks that the process responds.

\- \[ ] The PR passed CI.

\### Step 2: Merge to `main`

Merging triggers `ci.yml` and `cd.yml`. Wait for both to succeed.

\### Step 3: Identify the image tag

`cd.yml` publishes `sha-<short commit>` and `latest`. Note the SHA tag from the workflow output (the "Print image info" step) or from the GHCR package page.

\### Step 4: Trigger the rolling update

Preferred, with a pinned tag:

```powershell

kubectl -n ebay-clone set image deployment/ebay-api ebay-api=ghcr.io/monomee/ebay\_clone\_adminrole:sha-<short>

```

Alternative when the manifest uses `:latest` (`kubectl apply` with an unchanged tag does nothing, so force it):

```powershell

kubectl -n ebay-clone rollout restart deployment/ebay-api

```

\### Step 5: Monitor

```powershell

kubectl -n ebay-clone rollout status deployment/ebay-api

kubectl -n ebay-clone get pods -w

```

Expected behavior with 2 replicas:

1\. One new pod is created (surge to 3).

2\. The new pod passes the readiness probe and joins the Service.

3\. One old pod is terminated.

4\. Steps 1 to 3 repeat once more; the end state is 2 new pods.

\### Step 6: Verify

\- Through Nginx, call `/nginx-health` and `/health/ready` and expect HTTP 200.

\- Inspect the `X-Backend-Server` response header across several requests to see traffic reach the new pods.

\- Smoke test login and a SignalR feature (notifications or disputes) to confirm the Redis backplane works.

\- Check logs: `kubectl -n ebay-clone logs deployment/ebay-api --tail=100`.

\### Step 7: Roll back if needed

```powershell

kubectl -n ebay-clone rollout undo deployment/ebay-api

kubectl -n ebay-clone rollout status deployment/ebay-api

```

Rollback is itself a rolling replacement, so it is not instant. To target a specific revision, use `kubectl -n ebay-clone rollout history deployment/ebay-api` and `rollout undo --to-revision=<n>`.

\## 6. Failure handling

| Symptom | Meaning | Action |

|---|---|---|

| New pod stays `0/1 Running` | Readiness probe failing; rollout is stalled but old pods still serve users. | `kubectl -n ebay-clone describe pod <pod>` and `logs <pod>`; fix or `rollout undo`. |

| `ImagePullBackOff` | Registry access or wrong tag. | Check the tag and GHCR permissions. |

| `CrashLoopBackOff` | App fails at startup (config, secret, DB). | Check logs; verify ConfigMap and Secret values. |

| Errors only after full rollout | Bug not caught by the probe. | `rollout undo`, then fix forward. |

\## 7. Known limitations and recommended improvements

1\. \*\*No automated deploy step.\*\* `cd.yml` only pushes the image. Add a job that authenticates to the cluster, runs `set image` with the commit SHA, then `rollout status` to fail the pipeline if the rollout fails.

2\. \*\*Prefer SHA tags over `latest`\*\* in the deployment so rollouts and rollbacks are deterministic.

3\. \*\*Database compatibility\*\* is the main risk of this strategy. Use the expand/contract migration pattern, and confirm whether migrations run at app startup.

4\. \*\*Readiness probe.\*\* `/health/ready` runs `AddDbContextCheck` (`CanConnectAsync`). `/health/live` does not touch the database. See `docs/ExpandContractMigrations.md` for schema changes during a rollout.

5\. \*\*Database and Redis are single replicas.\*\* They are outside the rolling update but remain single points of failure.

6\. \*\*Graceful shutdown.\*\* `preStop` sleeps 15 seconds and `terminationGracePeriodSeconds` is 45. The host stops within 25 seconds so worker loops and SignalR close before SIGKILL.

