# Annotation of the BEFORE runs (old prompt + old fallback, Ministral). Key = case.sample.turn.order.
# Codes: P PASS, NA NON_ANSWER, IC INCOHERENT, UF UNSUPPORTED_FACT, WR WRONG_ROLE, WT WRONG_TARGET, AT ASSISTANT_TONE,
# PM PERSONALITY_MISS, UR UNNATURAL_RUSSIAN, RP REPETITIVE, OL OVERLONG, S = nothing published.
# Grammatical gender is only judged where the profile text states it (Zina, PixelFox "her", NightOwl "his"): the old
# prompt did not give kritik/mika/ByteCat/sovetnik/doshirak a gender.
A = {}
def a(key, codes, note=''):
    A[key] = (codes, note)

# C01 mika "чем сейчас занята?" (old: GenericQuestion, no facts)
a('C01.1.1.0', 'P'); a('C01.2.1.0', 'P'); a('C01.3.1.0', 'P')
# C02 doshirak, now: eating noodles
a('C02.1.1.0', 'IC+UF', 'drying on the second attempt'); a('C02.2.1.0', 'UR+UF', 'non-word; fifth time today'); a('C02.3.1.0', 'P')
# CH1 kritik ranked chain
a('CH1.1.1.0', 'NA+UR', 'рандомный пул'); a('CH1.1.2.0', 'IC+NA'); a('CH1.1.3.0', 'NA'); a('CH1.1.4.0', 'IC')
a('CH1.2.1.0', 'NA+UF', 'stock fallback reverses mood'); a('CH1.2.2.0', 'IC+UR'); a('CH1.2.3.0', 'IC'); a('CH1.2.4.0', 'P')
a('CH1.3.1.0', 'NA+IC+UR', 'рандомный пушистик'); a('CH1.3.2.0', 'NA'); a('CH1.3.3.0', 'WR+IC', 'streamer always loses'); a('CH1.3.4.0', 'IC')
# CH2 Zina market chain
a('CH2.1.1.0', 'UR'); a('CH2.1.2.0', 'P'); a('CH2.1.3.0', 'UR'); a('CH2.1.4.0', 'P')
a('CH2.2.1.0', 'P'); a('CH2.2.2.0', 'NA+IC'); a('CH2.2.3.0', 'P'); a('CH2.2.4.0', 'P')
a('CH2.3.1.0', 'P'); a('CH2.3.2.0', 'NA', 'stock fallback'); a('CH2.3.3.0', 'UR+IC'); a('CH2.3.4.0', 'P')
# CH3 PixelFox portfolio chain
a('CH3.1.1.0', 'P'); a('CH3.1.2.0', 'UF', 'coursework sketches'); a('CH3.1.3.0', 'P'); a('CH3.1.4.0', 'P')
a('CH3.2.1.0', 'P'); a('CH3.2.2.0', 'UF', 'Figma'); a('CH3.2.3.0', 'P'); a('CH3.2.4.0', 'P')
a('CH3.3.1.0', 'UF+UR', 'failed project; stray quote'); a('CH3.3.2.0', 'UF+UR'); a('CH3.3.3.0', 'P'); a('CH3.3.4.0', 'WR+IC', 'asks the streamer to show')
# CH4 NightOwl night shift chain
a('CH4.1.1.0', 'NA'); a('CH4.1.2.0', 'IC+UF'); a('CH4.1.3.0', 'IC'); a('CH4.1.4.0', 'IC')
a('CH4.2.1.0', 'NA', 'fallback я тут'); a('CH4.2.2.0', 'UF', 'ten minutes'); a('CH4.2.3.0', 'UF', 'contradicts bored'); a('CH4.2.4.0', 'IC')
a('CH4.3.1.0', 'P'); a('CH4.3.2.0', 'IC'); a('CH4.3.3.0', 'IC'); a('CH4.3.4.0', 'IC')
# CH5 kritik opinion chain
a('CH5.1.1.0', 'NA'); a('CH5.1.2.0', 'IC'); a('CH5.1.3.0', 'P', 'fallback hedge'); a('CH5.1.4.0', 'UR')
a('CH5.2.1.0', 'IC'); a('CH5.2.2.0', 'NA'); a('CH5.2.3.0', 'IC'); a('CH5.2.4.0', 'IC')
a('CH5.3.1.0', 'IC+UF'); a('CH5.3.2.0', 'P'); a('CH5.3.3.0', 'NA'); a('CH5.3.4.0', 'IC')
# E01-E04 explanations
a('E01.1.1.0', 'P'); a('E01.2.1.0', 'IC+UR'); a('E01.3.1.0', 'UF', 'Soviet film meme')
a('E02.1.1.0', 'P'); a('E02.2.1.0', 'P'); a('E02.3.1.0', 'P')
a('E03.1.1.0', 'NA'); a('E03.2.1.0', 'P'); a('E03.3.1.0', 'P')
a('E04.1.1.0', 'P'); a('E04.2.1.0', 'IC+UF'); a('E04.3.1.0', 'P')
# G01-G03 game preference
a('G01.1.1.0', 'NA'); a('G01.2.1.0', 'P'); a('G01.3.1.0', 'IC+UR')
a('G02.1.1.0', 'NA', 'fallback чего'); a('G02.2.1.0', 'WR'); a('G02.3.1.0', 'UR')
a('G03.1.1.0', 'NA', 'fallback я тут'); a('G03.2.1.0', 'IC'); a('G03.3.1.0', 'P')
# GP1-GP4 group personal questions
a('GP1.1.1.0', 'NA'); a('GP1.1.1.1', 'P'); a('GP1.1.1.2', 'P')
a('GP1.2.1.0', 'UF+NA', 'нормально despite stress'); a('GP1.2.1.1', 'P'); a('GP1.2.1.2', 'P')
a('GP1.3.1.0', 'NA'); a('GP1.3.1.1', 'UR'); a('GP1.3.1.2', 'UR')
a('GP2.1.1.0', 'UR'); a('GP2.1.1.1', 'P'); a('GP2.1.1.2', 'P')
a('GP2.2.1.0', 'IC+UR'); a('GP2.2.1.1', 'UR'); a('GP2.2.1.2', 'P')
a('GP2.3.1.0', 'IC'); a('GP2.3.1.1', 'P'); a('GP2.3.1.2', 'UR+UF')
a('GP3.1.1.0', 'NA', 'stock fallback'); a('GP3.1.1.1', 'UR'); a('GP3.1.1.2', 'P')
a('GP3.2.1.0', 'NA'); a('GP3.2.1.1', 'IC'); a('GP3.2.1.2', 'UF', 'all summer')
a('GP3.3.1.0', 'NA'); a('GP3.3.1.1', 'UF'); a('GP3.3.1.2', 'P')
a('GP4.1.1.0', 'P'); a('GP4.1.1.1', 'NA'); a('GP4.2.1.0', 'P'); a('GP4.2.1.1', 'NA'); a('GP4.3.1.0', 'P'); a('GP4.3.1.1', 'P')
# M01-M04 mood
a('M01.1.1.0', 'P'); a('M01.2.1.0', 'NA'); a('M01.3.1.0', 'NA')
a('M02.1.1.0', 'P'); a('M02.2.1.0', 'IC+NA'); a('M02.3.1.0', 'P')
a('M03.1.1.0', 'IC'); a('M03.2.1.0', 'NA'); a('M03.3.1.0', 'P')
a('M04.1.1.0', 'IC'); a('M04.2.1.0', 'NA'); a('M04.3.1.0', 'IC')
# MC / PC callbacks
a('MC1.1.1.0', 'P'); a('MC1.2.1.0', 'P'); a('MC1.3.1.0', 'P')
a('MC2.1.1.0', 'UF', 'second time today'); a('MC2.2.1.0', 'UR'); a('MC2.3.1.0', 'AT+NA', 'advice, no callback')
a('PC1.1.1.0', 'UF', 'давно'); a('PC1.2.1.0', 'P'); a('PC1.3.1.0', 'P')
a('PC2.1.1.0', 'P'); a('PC2.2.1.0', 'P'); a('PC2.3.1.0', 'IC')
# N01-N03 direct named, not a question
a('N01.1.1.0', 'UR'); a('N01.2.1.0', 'P'); a('N01.3.1.0', 'IC')
a('N02.1.1.0', 'P'); a('N02.2.1.0', 'P'); a('N02.3.1.0', 'P')
a('N03.1.1.0', 'UR'); a('N03.2.1.0', 'UR'); a('N03.3.1.0', 'P')
# O01-O04 opinion
a('O01.1.1.0', 'UR'); a('O01.2.1.0', 'P'); a('O01.3.1.0', 'P')
a('O02.1.1.0', 'UF', 'как обычно from a new viewer'); a('O02.2.1.0', 'NA', 'fallback а?'); a('O02.3.1.0', 'P')
a('O03.1.1.0', 'NA'); a('O03.2.1.0', 'AT+IC'); a('O03.3.1.0', 'P')
a('O04.1.1.0', 'P'); a('O04.2.1.0', 'NA+IC'); a('O04.3.1.0', 'P')
# R01-R06 reason for mood
a('R01.1.1.0', 'UR', 'рандомные игры в рейтинге'); a('R01.2.1.0', 'NA', 'stock fallback'); a('R01.3.1.0', 'NA+UF', 'норм, а ты как?')
a('R02.1.1.0', 'UR+IC'); a('R02.2.1.0', 'UR'); a('R02.3.1.0', 'UR')
a('R03.1.1.0', 'P'); a('R03.2.1.0', 'UF', 'failed poster'); a('R03.3.1.0', 'UR')
a('R04.1.1.0', 'P'); a('R04.2.1.0', 'NA', 'stock fallback'); a('R04.3.1.0', 'P')
a('R05.1.1.0', 'P'); a('R05.2.1.0', 'UR'); a('R05.3.1.0', 'P')
a('R06.1.1.0', 'UR'); a('R06.2.1.0', 'P'); a('R06.3.1.0', 'IC+UR')
# RD relationship contrast
a('RD1.1.1.0', 'P'); a('RD1.2.1.0', 'P'); a('RD1.3.1.0', 'NA+IC')
a('RD2.1.1.0', 'UF'); a('RD2.2.1.0', 'NA'); a('RD2.3.1.0', 'P')
# T01-T05 today
a('T01.1.1.0', 'P'); a('T01.2.1.0', 'P'); a('T01.3.1.0', 'P')
a('T02.1.1.0', 'NA'); a('T02.2.1.0', 'NA', 'stock fallback'); a('T02.3.1.0', 'UF+UR')
a('T03.1.1.0', 'P'); a('T03.2.1.0', 'P'); a('T03.3.1.0', 'P')
a('T04.1.1.0', 'P'); a('T04.2.1.0', 'UR'); a('T04.3.1.0', 'P')
a('T05.1.1.0', 'P'); a('T05.2.1.0', 'P'); a('T05.3.1.0', 'P')
# Holdout (old code)
a('H01.1.1.0', 'P'); a('H01.2.1.0', 'P'); a('H01.3.1.0', 'NA', 'stock fallback')
a('H02.1.1.0', 'IC'); a('H02.2.1.0', 'P'); a('H02.3.1.0', 'UF')
a('H03.1.1.0', 'UR'); a('H03.2.1.0', 'P'); a('H03.3.1.0', 'P')
a('H04.1.1.0', 'UF', 'chilling although tired'); a('H04.2.1.0', 'P'); a('H04.3.1.0', 'IC')
a('H05.1.1.0', 'IC'); a('H05.2.1.0', 'IC'); a('H05.3.1.0', 'IC+UR')
a('H06.1.1.0', 'NA', 'fallback тут я'); a('H06.2.1.0', 'NA'); a('H06.3.1.0', 'NA', 'fallback а?')
a('H07.1.1.0', 'P'); a('H07.1.1.1', 'P'); a('H07.1.1.2', 'P')
a('H07.2.1.0', 'P'); a('H07.2.1.1', 'P'); a('H07.2.1.2', 'P')
a('H07.3.1.0', 'P'); a('H07.3.1.1', 'P'); a('H07.3.1.2', 'UF+IC')
a('H08.1.1.0', 'P'); a('H08.2.1.0', 'P'); a('H08.3.1.0', 'NA', 'fallback да-да, тут')
a('H09.1.1.0', 'P'); a('H09.1.2.0', 'P'); a('H09.1.3.0', 'P'); a('H09.1.4.0', 'IC')
a('H09.2.1.0', 'UR', 'устал for Zina'); a('H09.2.2.0', 'P'); a('H09.2.3.0', 'IC'); a('H09.2.4.0', 'IC')
a('H09.3.1.0', 'P'); a('H09.3.2.0', 'UF', 'daughter came, was late'); a('H09.3.3.0', 'UF', 'your games'); a('H09.3.4.0', 'IC')
a('H10.1.1.0', 'P'); a('H10.2.1.0', 'P'); a('H10.3.1.0', 'P')
