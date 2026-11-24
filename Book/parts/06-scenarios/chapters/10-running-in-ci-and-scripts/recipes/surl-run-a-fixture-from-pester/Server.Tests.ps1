# Copyright (c) 2026 Stewart Rogers. Apache License 2.0. See Book/LICENSE-CODE.txt.
# Pester tests against a surl that BeforeAll starts on a free port and AfterAll stops, which
# Pester runs even when a test fails. The checks throw, so the file runs on Pester 5 and 3.4.
Describe 'the site served by surl' {
    BeforeAll {
        # Start surl on port 0 and read its first line, Listening on <url>, for the port it bound.
        Remove-Item surl.log -ErrorAction Ignore
        $start = [Diagnostics.ProcessStartInfo]::new((Get-Command surl -CommandType Application)[0].Source)
        foreach ($argument in '-v', '--log-file', 'surl.log', '--directory', 'site', 'http://127.0.0.1:0/') {
            $start.ArgumentList.Add($argument)
        }
        $start.RedirectStandardOutput = $true
        $surl = [Diagnostics.Process]::Start($start)
        $surl.Id | Set-Content surl.pid
        $line = $surl.StandardOutput.ReadLine()
        if ($line -notmatch '^Listening on (http://\S+)$') {
            $surl.WaitForExit()
            throw "surl did not start: exit $($surl.ExitCode)"
        }
        $url = $Matches[1]
    }

    AfterAll {
        # Stop surl however the tests went, and wait until it has gone.
        if ($surl -and -not $surl.HasExited) {
            $surl.Kill()
            $surl.WaitForExit()
        }
    }

    It 'listens on a port the system chose' {
        if ($url -notmatch '^http://127\.0\.0\.1:[1-9][0-9]*/$') { throw "unexpected URL $url" }
    }

    It 'serves the greeting' {
        $body = curl -fsS "${url}greeting.txt"
        $expected = Get-Content expected-greeting.txt
        if ($body -ne $expected) { throw "expected '$expected', got '$body'" }
    }

    It 'answers 404 for a missing file' {
        $status = curl -s -o missing.out -w '%{http_code}' "${url}missing.txt"
        if ($status -ne '404') { throw "expected 404, got $status" }
    }
}
