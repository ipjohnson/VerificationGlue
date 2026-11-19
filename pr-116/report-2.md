## Cold-build benchmark

10,000 tests and 101,002 `Verify` calls in 3 assemblies. Each build is a cold rebuild with build servers off, run 3 times per configuration in rotating order. Seconds are the median over runs of the summed build time of all assemblies. Added seconds are scaled to 100,000 calls. Machine: macOS 27.0.1, Arm64, 11 logical processors, .NET SDK 10.0.403.

| Build | Median seconds | Added seconds | Added seconds at 100,000 calls | Ratio to generator off | Interceptors | Copies | Redirects |
|---|---:|---:|---:|---:|---:|---:|---:|
| Generator off | 8.7 | 0.0 | 0.0 | 1.00 | 0 | 0 | 0 |
| Tier 2 alone | 95.1 | 86.5 | 85.6 | 10.96 | 101,002 | 0 | 0 |
| Tier 3 with redirects | 78.5 | 69.8 | 69.1 | 9.05 | 1,002 | 11,002 | 10,000 |

| Run | Build | Seconds per assembly | Total seconds |
|---|---|---|---:|
| 1 | Generator off | 2.8, 2.5, 2.5 | 7.8 |
| 1 | Tier 2 alone | 23.4, 27.0, 31.3 | 81.6 |
| 1 | Tier 3 with redirects | 21.8, 21.0, 19.7 | 62.6 |
| 2 | Tier 2 alone | 27.2, 63.1, 43.4 | 133.7 |
| 2 | Tier 3 with redirects | 38.5, 35.7, 24.2 | 98.3 |
| 2 | Generator off | 2.6, 3.4, 2.7 | 8.7 |
| 3 | Tier 3 with redirects | 24.4, 29.6, 24.5 | 78.5 |
| 3 | Generator off | 3.4, 3.2, 3.3 | 9.9 |
| 3 | Tier 2 alone | 28.1, 35.1, 31.9 | 95.1 |

### Budget

| Build | Measure | Limit | Measured | Result |
|---|---|---:|---:|---|
| Tier 2 alone | Added seconds at 100,000 calls | 160.00 | 85.60 | Passed |
| Tier 2 alone | Ratio to generator off | 8.50 | 10.96 | Over budget |
| Tier 3 with redirects | Added seconds at 100,000 calls | 120.00 | 69.15 | Passed |
| Tier 3 with redirects | Ratio to generator off | 7.00 | 9.05 | Over budget |
