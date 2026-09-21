import json
import os
import sys
import urllib.request

# Токен доступа к VDS через redl.io — только из переменной окружения REDL_TOKEN:
# в репозитории секретов быть не должно (прежний токен из истории git отозвать).
TOKEN = os.environ.get("REDL_TOKEN", "")
if not TOKEN:
    sys.exit("задайте REDL_TOKEN (токен redl.io) в переменной окружения")
HEADERS = {"Authorization": f"Bearer {TOKEN}", "Content-Type": "application/json"}

def run_vds(command, workdir=None, timeout=60):
    payload = {"machine": "avds-rg1s7j", "command": command, "timeoutSec": timeout}
    if workdir:
        payload["workdir"] = workdir
    req = urllib.request.Request("https://redl.io/api/ext/run", headers=HEADERS, data=json.dumps(payload).encode())
    with urllib.request.urlopen(req) as resp:
        return json.loads(resp.read().decode())

def write_vds(path, content):
    payload = {"machine": "avds-rg1s7j", "path": path, "content": content}
    req = urllib.request.Request("https://redl.io/api/ext/write", headers=HEADERS, data=json.dumps(payload).encode())
    with urllib.request.urlopen(req) as resp:
        return json.loads(resp.read().decode())

if __name__ == "__main__":
    sys.stdout.reconfigure(encoding="utf-8")
    cmd = sys.argv[1] if len(sys.argv) > 1 else "uptime"
    res = run_vds(cmd)
    print("ExitCode:", res.get("exitCode"))
    if res.get("output"):
        print("Output:\n" + res["output"])
    if res.get("stderr"):
        print("Stderr:\n" + res["stderr"])
