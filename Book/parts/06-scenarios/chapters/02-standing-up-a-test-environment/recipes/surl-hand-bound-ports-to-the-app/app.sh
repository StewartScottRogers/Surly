#!/bin/sh
# Stands in for the app under test: it finds both servers through the environment alone.
greeting=$(curl -fsS "${APP_HTTP_URL}greeting.txt") || exit
printf 'Subject: Welcome\r\n\r\n%s\r\n' "$greeting" |
  curl -sS -u app@example.com:secret --mail-from app@example.com --mail-rcpt app@example.com \
    -T - "${APP_SMTP_URL}example.com" || exit
echo "the app mailed: $greeting"
