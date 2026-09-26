import json, sys, time, urllib.request

# Usage: probe.py model [system_suffix] [max_tokens]; sends the real R01 answer prompt from the final Ministral run.
model = sys.argv[1]
suffix = sys.argv[2] if len(sys.argv) > 2 else ''
max_tokens = int(sys.argv[3]) if len(sys.argv) > 3 else 44
rows = [json.loads(l) for l in open(r'E:/GO-Live-ConversationQuality-20260926/after2/after2-ministral-20260926-203127.jsonl', encoding='utf-8')]
row = next(r for r in rows if r['record'] == 'outcome' and r['caseId'] == 'R01' and r['sample'] == 1)
schema = {"type": "json_schema", "json_schema": {"name": "viewer_message", "strict": True, "schema": {"type": "object",
          "properties": {"text": {"type": "string", "maxLength": 220}}, "required": ["text"], "additionalProperties": False}}}
body = {"model": model, "messages": [{"role": "system", "content": row['systemPrompt'] + suffix}, {"role": "user", "content": row['userPrompt']}],
        "temperature": 0.85, "top_p": 0.95, "max_tokens": max_tokens, "stream": False, "response_format": schema}
for attempt in range(3):
    started = time.time()
    request = urllib.request.Request('http://127.0.0.1:1234/v1/chat/completions', data=json.dumps(body).encode('utf-8'),
                                     headers={'Content-Type': 'application/json'})
    with urllib.request.urlopen(request, timeout=120) as response:
        data = json.loads(response.read().decode('utf-8'))
    message = data['choices'][0]['message']
    print(f"{time.time() - started:.2f}s finish={data['choices'][0].get('finish_reason')} usage={data.get('usage')}")
    print('  content:', repr(message.get('content'))[:300])
    if message.get('reasoning_content') or message.get('reasoning'):
        print('  reasoning:', repr(message.get('reasoning_content') or message.get('reasoning'))[:300])
