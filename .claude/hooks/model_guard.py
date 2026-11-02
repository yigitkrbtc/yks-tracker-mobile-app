#!/usr/bin/env python3
"""PreToolUse(Agent) kancası: alt ajan model/efor kısıtını zorlar (ADR-0020).

İzinli: sonnet/opus, efor medium/high. xhigh yalnız istemde `XHIGH-GEREKCE:` varsa.
Reddedilen: haiku, fable, diğer modeller, low ve max efor, model belirtmeyen yerleşik ajan.
Çıkış 2 = çağrı engellenir, stderr Claude'a döner.
"""
import json
import os
import re
import sys

ALLOWED_MODELS = {"sonnet", "opus"}
ALLOWED_EFFORTS = {"medium", "high"}
XHIGH_MARKER = "XHIGH-GEREKCE:"
INHERITING_TYPES = {"fork"}  # üst oturumun modelini devralır


def deny(msg: str) -> None:
    print(f"model-guard: {msg} (bkz. docs/surec/operasyon.md)", file=sys.stderr)
    sys.exit(2)


def frontmatter(path: str) -> dict:
    try:
        with open(path, encoding="utf-8") as f:
            text = f.read()
    except OSError:
        return {}
    m = re.match(r"^---\n(.*?)\n---", text, re.S)
    if not m:
        return {}
    out = {}
    for line in m.group(1).splitlines():
        if ":" in line:
            k, v = line.split(":", 1)
            out[k.strip()] = v.strip()
    return out


def model_family(name: str) -> str:
    n = (name or "").lower()
    for fam in ("opus", "sonnet", "haiku", "fable"):
        if fam in n:
            return fam
    return n


def check_effort(effort: str, prompt: str, source: str) -> None:
    e = (effort or "").lower()
    if not e or e in ALLOWED_EFFORTS:
        return
    if e == "xhigh":
        if XHIGH_MARKER in prompt:
            return
        deny(f"{source} effort=xhigh için istemde '{XHIGH_MARKER} <gerekçe>' gerekir (Yiğit onaylı)")
    deny(f"{source} effort={e} yasak; yalnız medium/high (xhigh gerekçeyle)")


def main() -> None:
    try:
        data = json.load(sys.stdin)
    except (json.JSONDecodeError, ValueError):
        return
    if data.get("tool_name") not in ("Agent", "Task"):
        return
    ti = data.get("tool_input") or {}
    prompt = ti.get("prompt") or ""
    agent_type = ti.get("subagent_type") or ""
    model = ti.get("model")

    project_dir = os.environ.get("CLAUDE_PROJECT_DIR") or data.get("cwd") or "."
    agent_file = os.path.join(project_dir, ".claude", "agents", f"{agent_type}.md") if agent_type else ""
    fm = frontmatter(agent_file) if agent_file else {}

    if model:
        if model_family(model) not in ALLOWED_MODELS:
            deny(f"model={model} yasak; yalnız sonnet/opus")
    elif fm:
        if model_family(fm.get("model", "")) not in ALLOWED_MODELS:
            deny(f"{agent_type} tanımındaki model '{fm.get('model')}' yasak; yalnız sonnet/opus")
    elif agent_type not in INHERITING_TYPES:
        deny(f"'{agent_type or 'varsayılan'}' ajanı proje ajanı değil; model=sonnet|opus ver "
             "veya .claude/agents altındaki bir rolü kullan")

    if ti.get("effort"):
        check_effort(ti["effort"], prompt, "çağrı")
    elif fm:
        check_effort(fm.get("effort", ""), prompt, f"{agent_type} tanımı")


if __name__ == "__main__":
    main()
