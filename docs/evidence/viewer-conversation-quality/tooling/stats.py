import csv, glob, json, os, statistics, sys
sys.path.insert(0, os.path.dirname(__file__))
import ann_before, ann_after2, ann_models, ann_gemma3, ann_after1_holdout

ROOT = 'E:/GO-Live-ConversationQuality-20260926'
OUT = os.path.join(os.path.dirname(__file__), 'evidence')
os.makedirs(OUT, exist_ok=True)
LABELS = ['NA', 'IC', 'UF', 'WR', 'WT', 'AT', 'PM', 'UR', 'RP', 'OL']
NAMES = {'NA': 'NON_ANSWER', 'IC': 'INCOHERENT', 'UF': 'UNSUPPORTED_FACT', 'WR': 'WRONG_ROLE', 'WT': 'WRONG_TARGET',
         'AT': 'ASSISTANT_TONE', 'PM': 'PERSONALITY_MISS', 'UR': 'UNNATURAL_RUSSIAN', 'RP': 'REPETITIVE', 'OL': 'OVERLONG'}
# Direct personal/factual questions: the viewer is asked about their own state or day (fixed by case/turn, not by
# how a given code version classified it).
DIRECT = {('R0%d' % i, 1) for i in range(1, 7)} | {('T0%d' % i, 1) for i in range(1, 6)} | {('M0%d' % i, 1) for i in range(1, 5)} | \
         {('C01', 1), ('C02', 1), ('RD1', 1), ('RD2', 1), ('GP1', 1), ('GP2', 1), ('GP3', 1), ('GP4', 1), ('E04', 1),
          ('CH1', 1), ('CH2', 1), ('CH2', 2), ('CH3', 1), ('CH3', 2), ('CH4', 1),
          ('H01', 1), ('H02', 1), ('H03', 1), ('H04', 1), ('H05', 1), ('H06', 1), ('H07', 1), ('H08', 1), ('H09', 1), ('H09', 2), ('H09', 3)}
RUNS = [
    ('before (dev corpus)', 'before/before-ministral-*.jsonl', ann_before.A, lambda k: not k.startswith('H')),
    ('before (holdout)', 'before/before-holdout-ministral-*.jsonl', ann_before.A, lambda k: k.startswith('H')),
    ('after-1 (holdout, clean)', 'after/after-ministral-*.jsonl', ann_after1_holdout.A, lambda k: k.startswith('H')),
    ('after (dev corpus)', 'after2/after2-ministral-*.jsonl', ann_after2.A, lambda k: not k.startswith('H')),
    ('after (holdout)', 'after2/after2-ministral-*.jsonl', ann_after2.A, lambda k: k.startswith('H')),
    ('qwen3-8b (dev corpus)', 'models/after2-qwen3-8b-*.jsonl', ann_models.QWEN8, lambda k: not k.startswith('H')),
    ('qwen3-8b (holdout)', 'models/after2-qwen3-8b-*.jsonl', ann_models.QWEN8, lambda k: k.startswith('H')),
    ('ministral, sample 1 only', 'after2/after2-ministral-*.jsonl', ann_after2.A, lambda k: k.split('.')[1] == '1'),
    ('qwen3-8b, sample 1 only', 'models/after2-qwen3-8b-*.jsonl', ann_models.QWEN8, lambda k: k.split('.')[1] == '1'),
    ('gemma-3-4b (dev corpus)', 'models-n3/after2-gemma-3-4b-n3-*.jsonl', ann_gemma3.A, lambda k: not k.startswith('H')),
    ('gemma-3-4b (holdout)', 'models-n3/after2-gemma-3-4b-n3-*.jsonl', ann_gemma3.A, lambda k: k.startswith('H')),
    ('gemma-3-4b screen, 1 sample', 'models/after2-gemma-3-4b-*.jsonl', ann_models.GEMMA, lambda k: True),
    ('qwen3-4b-2507, 1 sample', 'models/after2-qwen3-4b-2507-*.jsonl', ann_models.QWEN4, lambda k: True),
]


def load(pattern):
    path = sorted(glob.glob(os.path.join(ROOT, pattern)))[-1]
    rows = [json.loads(l) for l in open(path, encoding='utf-8')]
    return path, [r for r in rows if r['record'] == 'outcome']


def key(r):
    return f"{r['caseId']}.{r['sample']}.{r['turn']}.{r['order']}"


def pct(n, d):
    return f"{n}/{d} ({100.0 * n / d:.0f}%)" if d else '0/0'


report = []
for title, pattern, ann, keep in RUNS:
    path, rows = load(pattern)
    rows = [r for r in rows if keep(key(r))]
    missing = [key(r) for r in rows if key(r) not in ann]
    shown_keys = {key(r) for r in rows}
    extra = [k for k in ann if keep(k) and k not in shown_keys]
    assert not missing, (title, missing[:10])
    assert not extra, (title, extra[:10])
    counts = {l: 0 for l in LABELS}
    passed = silent = fallback = stock = 0
    direct = direct_pass = direct_na = 0
    for r in rows:
        codes = ann[key(r)][0].split('+')
        shown = r['outcome'] == 'Shown'
        if codes == ['S']:
            assert not shown, (title, key(r), 'annotated silent but published')
            silent += 1
        else:
            assert shown, (title, key(r), 'annotated but not published')
        if codes == ['P']: passed += 1
        for c in codes:
            if c in counts: counts[c] += 1
        if shown and r['publishedSource'] == 'Fallback':
            fallback += 1
            if 'NA' in codes: stock += 1
        if (r['caseId'], r['turn']) in DIRECT:
            direct += 1
            if codes == ['P']: direct_pass += 1
            if 'NA' in codes: direct_na += 1
    lat = sorted(r['latencySeconds'] for r in rows if r['status'] == 'Ok')
    chains = {}
    for r in rows:
        if r['group'] == 'Chain' or r['caseId'] == 'H09':
            chains.setdefault((r['caseId'], r['sample']), []).append(ann[key(r)][0])
    coherent = sum(1 for v in chains.values() if all(c == 'P' for c in v))
    report.append({
        'run': title, 'file': os.path.basename(path), 'replies': len(rows), 'pass': passed, 'silent': silent,
        'fallback': fallback, 'stock_fallback': stock, 'direct': direct, 'direct_pass': direct_pass, 'direct_na': direct_na,
        'chains': len(chains), 'chains_all_pass': coherent,
        'lat_mean': statistics.mean(lat) if lat else 0, 'lat_median': statistics.median(lat) if lat else 0,
        'lat_p90': lat[int(0.9 * (len(lat) - 1))] if lat else 0, 'lat_max': lat[-1] if lat else 0, **counts})
    slug = title.replace(' ', '_').replace('(', '').replace(')', '').replace(',', '')
    with open(os.path.join(OUT, 'annotated-' + slug + '.csv'), 'w', encoding='utf-8', newline='') as handle:
        w = csv.writer(handle)
        w.writerow(['key', 'viewer', 'purpose', 'streamer', 'previous_line', 'answer_facts', 'raw_model_text', 'validation', 'published_source',
                    'published_text', 'labels', 'note'])
        for r in sorted(rows, key=lambda r: (r['caseId'], r['sample'], r['turn'], r['order'])):
            codes, note = ann[key(r)]
            w.writerow([key(r), r['viewerName'], r['purpose'], r['speech'], r.get('previousViewerLine') or '', ' | '.join(r.get('answerFacts') or []),
                        r['rawModelText'], r.get('validationReason') or '', r['publishedSource'] if r['outcome'] == 'Shown' else 'none',
                        r['publishedText'] if r['outcome'] == 'Shown' else '', '+'.join(NAMES.get(c, 'PASS' if c == 'P' else 'SILENT') for c in codes.split('+')), note])

head = ['run', 'replies', 'PASS', 'silent', 'fallback (stock/unrelated)', 'direct Q PASS', 'direct Q NON_ANSWER', 'chains all-PASS'] + [NAMES[l] for l in LABELS] + \
       ['latency mean/median/p90/max s']
print('| ' + ' | '.join(head) + ' |')
print('|' + '---|' * len(head))
for x in report:
    print('| ' + ' | '.join([x['run'], str(x['replies']), pct(x['pass'], x['replies']), str(x['silent']), f"{x['fallback']} ({x['stock_fallback']})",
                             pct(x['direct_pass'], x['direct']), pct(x['direct_na'], x['direct']), f"{x['chains_all_pass']}/{x['chains']}"] +
                            [str(x[l]) for l in LABELS] + [f"{x['lat_mean']:.3f} / {x['lat_median']:.3f} / {x['lat_p90']:.3f} / {x['lat_max']:.3f}"]) + ' |')
json.dump(report, open(os.path.join(OUT, 'summary.json'), 'w', encoding='utf-8'), indent=1)
