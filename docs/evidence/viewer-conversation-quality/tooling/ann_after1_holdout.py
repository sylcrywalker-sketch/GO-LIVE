# Holdout lines of the FIRST post-change run (after-1), before the round-2 fixes: the clean holdout measurement.
A = {}
def a(key, codes, note=''): A[key] = (codes, note)

a('H01.1.1.0', 'P'); a('H01.2.1.0', 'P'); a('H01.3.1.0', 'P')
a('H02.1.1.0', 'P'); a('H02.2.1.0', 'P'); a('H02.3.1.0', 'UR')
a('H03.1.1.0', 'P'); a('H03.2.1.0', 'P'); a('H03.3.1.0', 'UR+UF', 'не очень despite chill')
a('H04.1.1.0', 'P'); a('H04.2.1.0', 'P'); a('H04.3.1.0', 'P')
a('H05.1.1.0', 'IC'); a('H05.2.1.0', 'IC'); a('H05.3.1.0', 'UF', 'on shift despite day off (no facts supplied yet)')
a('H06.1.1.0', 'P'); a('H06.2.1.0', 'P'); a('H06.3.1.0', 'P')
a('H07.1.1.0', 'P'); a('H07.1.1.1', 'P'); a('H07.1.1.2', 'P')
a('H07.2.1.0', 'P', 'learner Russian fits Jonas'); a('H07.2.1.1', 'NA+AT', 'lecture instead of own day'); a('H07.2.1.2', 'P')
a('H07.3.1.0', 'P'); a('H07.3.1.1', 'P'); a('H07.3.1.2', 'P')
a('H08.1.1.0', 'IC'); a('H08.2.1.0', 'P'); a('H08.3.1.0', 'UR')
a('H09.1.1.0', 'P'); a('H09.1.2.0', 'IC'); a('H09.1.3.0', 'UR', 'Latin letter'); a('H09.1.4.0', 'P')
a('H09.2.1.0', 'WR+UR', 'calls the streamer доченька'); a('H09.2.2.0', 'P'); a('H09.2.3.0', 'P'); a('H09.2.4.0', 'P')
a('H09.3.1.0', 'P'); a('H09.3.2.0', 'P'); a('H09.3.3.0', 'P', 'purpose fallback'); a('H09.3.4.0', 'IC')
a('H10.1.1.0', 'P'); a('H10.2.1.0', 'NA'); a('H10.3.1.0', 'P')
