# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Runs the Pester tests and exits with the number that failed, so a CI step fails with them.
$result = Invoke-Pester -Path ./Server.Tests.ps1 -PassThru
"passed $($result.PassedCount), failed $($result.FailedCount)"
exit $result.FailedCount
