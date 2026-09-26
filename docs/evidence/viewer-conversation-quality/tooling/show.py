import json, sys, statistics

# Usage: show.py run.jsonl [compact]
path = sys.argv[1]
compact = len(sys.argv) > 2
rows = [json.loads(l) for l in open(path, encoding='utf-8')]
out = [r for r in rows if r['record'] == 'outcome']
man = [r for r in rows if r['record'] == 'manifest'][0]
print('model', man['model'], '|', man['notes'][:300])
lat = [r['latencySeconds'] for r in out if r['status'] == 'Ok']
print(f"rows={len(out)} modelOK={sum(1 for r in out if r['status']=='Ok')} accepted={sum(1 for r in out if r['accepted'])} "
      f"shownModel={sum(1 for r in out if r['outcome']=='Shown' and r['publishedSource']=='LanguageModel')} "
      f"shownFallback={sum(1 for r in out if r['outcome']=='Shown' and r['publishedSource']=='Fallback')} "
      f"discarded={sum(1 for r in out if r['outcome']!='Shown')}")
if lat:
    lat.sort()
    print(f"latency mean={statistics.mean(lat):.3f} median={statistics.median(lat):.3f} p90={lat[int(0.9*(len(lat)-1))]:.3f} max={lat[-1]:.3f}")
key = lambda r: (r['caseId'], r['sample'], r['turn'], r['order'])
last = None
for r in sorted(out, key=key):
    head = (r['caseId'], r['sample'])
    if head != last:
        print()
        last = head
    facts = '; '.join(r.get('answerFacts') or [])
    pub = r['publishedText'] if r['outcome'] == 'Shown' else '—'
    src = {'LanguageModel': 'M', 'Fallback': 'F'}.get(r['publishedSource'], r['publishedSource'])
    rej = '' if r['accepted'] else f" [REJ {r['validationReason']}]"
    tag = f"{r['caseId']}.{r['sample']}.t{r['turn']}.o{r['order']}"
    if compact:
        print(f"{tag} {r['viewerName'][:12]:12} {r['purpose'][:14]:14} «{r['speech']}» -> raw «{r['rawModelText']}»{rej} => {src}:«{pub}»")
    else:
        print(f"{tag} {r['viewerName']} tier={r['tier']} day={r['dayId']} purpose={r['purpose']} intent={r['plannedIntent']} target={r['targetKind']}")
        print(f"    speech «{r['speech']}» prev «{r.get('previousViewerLine') or ''}» facts [{facts}]")
        print(f"    raw «{r['rawModelText']}»{rej} => {r['outcome']} {src}:«{pub}» ({r['latencySeconds']:.2f}s)")
