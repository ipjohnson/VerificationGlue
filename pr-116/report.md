## Cold-build benchmark

10,000 tests and 101,002 `Verify` calls in 3 assemblies. Each build is a cold rebuild with build servers off, run 3 times per configuration in rotating order. Seconds are the median over runs of the summed build time of all assemblies. Added seconds are scaled to 100,000 calls. Machine: macOS 27.0.1, Arm64, 11 logical processors, .NET SDK 10.0.403.

| Build | Median seconds | Added seconds | Added seconds at 100,000 calls | Ratio to generator off | Interceptors | Copies | Redirects |
|---|---:|---:|---:|---:|---:|---:|---:|
| Generator off | 11.5 | 0.0 | 0.0 | 1.00 | 0 | 0 | 0 |
| Tier 2 alone | 88.1 | 76.6 | 75.8 | 7.65 | 101,002 | 0 | 0 |
| Tier 3 with redirects | 73.2 | 61.7 | 61.1 | 6.35 | 1,002 | 11,002 | 10,000 |

| Run | Build | Seconds per assembly | Total seconds |
|---|---|---|---:|
| 1 | Generator off | 3.4, 4.3, 3.8 | 11.5 |
| 1 | Tier 2 alone | 29.5, 25.6, 33.0 | 88.1 |
| 1 | Tier 3 with redirects | 25.2, 32.2, 27.2 | 84.6 |
| 2 | Tier 2 alone | 29.3, 41.0, 38.5 | 108.8 |
| 2 | Tier 3 with redirects | 17.9, 29.5, 25.9 | 73.2 |
| 2 | Generator off | 3.9, 4.1, 3.6 | 11.6 |
| 3 | Tier 3 with redirects | 21.1, 19.5, 19.7 | 60.3 |
| 3 | Generator off | 2.6, 2.3, 2.5 | 7.4 |
| 3 | Tier 2 alone | 24.0, 22.6, 28.3 | 74.8 |

### Budget

| Build | Measure | Limit | Measured | Result |
|---|---|---:|---:|---|
| Tier 2 alone | Added seconds at 100,000 calls | 160.00 | 75.85 | Passed |
| Tier 2 alone | Ratio to generator off | 8.50 | 7.65 | Passed |
| Tier 3 with redirects | Added seconds at 100,000 calls | 120.00 | 61.07 | Passed |
| Tier 3 with redirects | Ratio to generator off | 7.00 | 6.35 | Passed |
