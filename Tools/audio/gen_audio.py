#!/usr/bin/env python3
# -*- coding: utf-8 -*-
"""
Gerador de áudio do Drakantus 3D.
Todo o áudio é SINTETIZADO aqui (100% original, sem samples de terceiros).

Saída: Assets/Drakantus/Resources/Audio/<id>.wav
  - Efeitos: WAV 16-bit mono 44100 Hz
  - Músicas: WAV 16-bit mono 22050 Hz, loops perfeitos (comprimento exato em
    compassos; caudas de notas e reverb "dão a volta" para o início do loop).

Uso:  python3 Tools/audio/gen_audio.py              (tudo)
      python3 Tools/audio/gen_audio.py sfx2 music   (efeitos v2 + músicas por área)
      python3 Tools/audio/gen_audio.py new          (habilidades + ultimates + combo)
      python3 Tools/audio/gen_audio.py skills ults combo sk_meteor ...
Requer numpy (scipy é opcional, só acelera os filtros IIR).
"""
import os
import sys
import math
import wave
import numpy as np

try:
    from scipy.signal import lfilter as _sp_lfilter
except Exception:  # scipy ausente: implementação lenta, mas funcional
    _sp_lfilter = None

ROOT = os.path.abspath(os.path.join(os.path.dirname(__file__), "..", ".."))
OUT = os.path.join(ROOT, "Assets", "Drakantus", "Resources", "Audio")
SR = 44100      # efeitos
MSR = 22050     # músicas
rng = np.random.default_rng(1337)


# ---------------------------------------------------------------------------
# Utilitários básicos de DSP
# ---------------------------------------------------------------------------
def lfilter(b, a, x):
    """Filtro IIR genérico (scipy se houver, senão laço em Python)."""
    if _sp_lfilter is not None:
        return _sp_lfilter(b, a, x)
    b = list(b); a = list(a)
    a0 = a[0]
    b = [v / a0 for v in b]; a = [v / a0 for v in a]
    y = np.zeros(len(x)); xs = list(x)
    nb, na = len(b), len(a)
    for n in range(len(xs)):
        acc = 0.0
        for k in range(nb):
            if n - k >= 0:
                acc += b[k] * xs[n - k]
        for k in range(1, na):
            if n - k >= 0:
                acc -= a[k] * y[n - k]
        y[n] = acc
    return y


def T(dur, sr=SR):
    return np.arange(int(dur * sr)) / sr


def noise(dur, sr=SR):
    return rng.uniform(-1, 1, int(dur * sr))


def lp1(x, fc, sr=SR):
    """Passa-baixa de 1 polo."""
    a = math.exp(-2 * math.pi * fc / sr)
    return lfilter([1 - a], [1, -a], x)


def hp1(x, fc, sr=SR):
    """Passa-alta de 1 polo (x - passa-baixa)."""
    return x - lp1(x, fc, sr)


def biquad(x, fc, q=0.707, kind="lp", sr=SR):
    """Biquad RBJ (lp, hp, bp)."""
    w = 2 * math.pi * min(fc, sr * 0.45) / sr
    cw, sw = math.cos(w), math.sin(w)
    al = sw / (2 * q)
    if kind == "lp":
        b = [(1 - cw) / 2, 1 - cw, (1 - cw) / 2]
    elif kind == "hp":
        b = [(1 + cw) / 2, -(1 + cw), (1 + cw) / 2]
    else:  # bp com ganho de pico 0 dB
        b = [al, 0, -al]
    a = [1 + al, -2 * cw, 1 - al]
    return lfilter(b, a, x)


def svf(x, fc, q=2.0, mode="bp", sr=SR):
    """Filtro de estado variável (Chamberlin) com corte variando por amostra.
    fc pode ser escalar ou array (varredura de filtro = whoosh)."""
    n = len(x)
    fc = np.broadcast_to(np.asarray(fc, dtype=float), (n,))
    f = 2 * np.sin(np.pi * np.clip(fc, 20, sr / 7.0) / sr)
    damp = 1.0 / q
    low = band = 0.0
    out = np.zeros(n)
    xs = x.tolist(); fs = f.tolist()
    if mode == "bp":
        for i in range(n):
            low += fs[i] * band
            high = xs[i] - low - damp * band
            band += fs[i] * high
            out[i] = band
    elif mode == "lp":
        for i in range(n):
            low += fs[i] * band
            high = xs[i] - low - damp * band
            band += fs[i] * high
            out[i] = low
    else:
        for i in range(n):
            low += fs[i] * band
            high = xs[i] - low - damp * band
            band += fs[i] * high
            out[i] = high
    return out


def osc(freq, dur=None, sr=SR, shape="sine", phase0=0.0):
    """Oscilador com frequência escalar ou array (glide)."""
    if np.isscalar(freq):
        freq = np.full(int(dur * sr), float(freq))
    ph = 2 * np.pi * np.cumsum(freq) / sr + phase0
    if shape == "sine":
        return np.sin(ph)
    if shape == "tri":
        return 2 / np.pi * np.arcsin(np.sin(ph))
    if shape == "square":
        return np.tanh(3 * np.sin(ph))  # quadrada suave (menos aliasing/estridência)
    raise ValueError(shape)


def additive_saw(freq, dur=None, sr=SR, bright=1.0, maxh=40):
    """Dente-de-serra sem aliasing (soma de harmônicos até ~0.45*sr)."""
    if np.isscalar(freq):
        freq = np.full(int(dur * sr), float(freq))
    ph = 2 * np.pi * np.cumsum(freq) / sr
    fmax = float(np.max(freq))
    out = np.zeros(len(freq))
    for k in range(1, maxh + 1):
        if k * fmax > sr * 0.45:
            break
        out += np.sin(k * ph) / (k ** (1.0 + (1.0 - bright)))
    return out * 0.6


def fm(fc, ratio, index, dur, sr=SR, index_env=None):
    """FM simples (2 operadores) para sinos e metais."""
    t = T(dur, sr)
    idx = index if index_env is None else index * index_env
    mod = np.sin(2 * np.pi * fc * ratio * t)
    return np.sin(2 * np.pi * fc * t + idx * mod)


def env_exp(dur, decay, sr=SR, attack=0.002):
    """Envelope ataque linear + decaimento exponencial (decay = constante de tempo)."""
    t = T(dur, sr)
    e = np.exp(-t / decay)
    na = max(1, int(attack * sr))
    e[:na] *= np.linspace(0, 1, na)
    return e


def adsr(dur, a, d, s, r, sr=SR):
    n = int(dur * sr)
    na, nd, nr = int(a * sr), int(d * sr), int(r * sr)
    if na + nd + nr > n:  # nota curta: comprime A/D/R para caber (evita corte seco = clique)
        k = n / float(na + nd + nr)
        na, nd, nr = int(na * k), int(nd * k), int(nr * k)
    ns = max(0, n - na - nd - nr)
    e = np.concatenate([
        np.linspace(0, 1, max(na, 1)),
        np.linspace(1, s, max(nd, 1)),
        np.full(ns, s),
        np.linspace(s, 0, max(nr, 1)),
    ])
    return fit(e, n)


def fit(x, n):
    if len(x) >= n:
        return x[:n]
    return np.concatenate([x, np.zeros(n - len(x))])


def mix(*sigs):
    n = max(len(s) for s in sigs)
    out = np.zeros(n)
    for s in sigs:
        out[:len(s)] += s
    return out


def delay_sig(x, sec, sr=SR):
    return np.concatenate([np.zeros(int(sec * sr)), x])


def crackle(dur, density, sr=SR, decay=0.004):
    """Estalos aleatórios (fogo, ossos): impulsos esparsos com micro-decaimento."""
    n = int(dur * sr)
    imp = np.zeros(n)
    mask = rng.random(n) < density / sr
    imp[mask] = rng.uniform(-1, 1, mask.sum())
    k = env_exp(decay * 6, decay, sr, attack=0.0001)
    return np.convolve(imp, k * rng.uniform(-1, 1, len(k)))[:n]


def fades(x, fin=0.002, fout=0.012, sr=SR):
    x = x.copy()
    ni, no = min(int(fin * sr), len(x)), min(int(fout * sr), len(x))
    if ni > 0:
        x[:ni] *= np.linspace(0, 1, ni)
    if no > 0:
        x[-no:] *= np.linspace(1, 0, no)
    return x


def normalize(x, level=1.0, peak_db=-1.0):
    """Pico em -1 dBFS multiplicado por 'level' (coerência entre efeitos)."""
    x = x - np.mean(x)
    p = np.max(np.abs(x))
    if p < 1e-9:
        return x
    return x / p * (10 ** (peak_db / 20.0)) * level


def write_wav(name, x, sr):
    path = os.path.join(OUT, name + ".wav")
    x = np.clip(x, -1, 1)
    data = (x * 32767).astype("<i2").tobytes()
    with wave.open(path, "wb") as w:
        w.setnchannels(1)
        w.setsampwidth(2)
        w.setframerate(sr)
        w.writeframes(data)
    return path


def mtof(m):
    return 440.0 * 2 ** ((m - 69) / 12.0)


def ks_pluck(freq, dur, sr=MSR, t60=1.2, bright=0.6):
    """Karplus-Strong vetorizado por blocos de período (alaúde / harpa)."""
    P = max(2, int(round(sr / freq - 0.5)))
    n = int(dur * sr) + P + 1
    y = np.zeros(n)
    exc = rng.uniform(-1, 1, P + 1)
    # brilho do ataque: suaviza a excitação
    a = 1.0 - bright
    for _ in range(2):
        exc = lfilter([1 - a], [1, -a], exc) if a > 0 else exc
    y[:P + 1] = exc / (np.max(np.abs(exc)) + 1e-9)
    d = 10 ** (-3.0 / (t60 * freq))
    start = P + 1
    while start < n:
        end = min(start + P, n)
        y[start:end] = d * 0.5 * (y[start - P:end - P] + y[start - P - 1:end - P - 1])
        start = end
    out = y[:int(dur * sr)]
    return fades(out, 0.001, 0.02, sr)


# ---------------------------------------------------------------------------
# EFEITOS SONOROS (44100 Hz)
# ---------------------------------------------------------------------------
def whoosh(dur, f0, f1, f2, q=1.6, peak_at=0.45, sr=SR):
    """Ruído com passa-banda varrendo f0 -> f1 -> f2 (espada cortando o ar)."""
    n = int(dur * sr)
    t = np.linspace(0, 1, n)
    fc = np.where(t < peak_at,
                  f0 + (f1 - f0) * (t / peak_at) ** 1.5,
                  f1 + (f2 - f1) * ((t - peak_at) / (1 - peak_at)))
    e = np.where(t < peak_at, (t / peak_at) ** 2, np.exp(-(t - peak_at) * 7))
    return svf(noise(dur, sr), fc, q, "bp", sr) * e


def thump(dur, f0, f1, decay, sr=SR):
    """Impacto grave: seno com queda rápida de frequência."""
    n = int(dur * sr)
    t = np.arange(n) / sr
    f = f1 + (f0 - f1) * np.exp(-t / 0.03)
    return osc(f, sr=sr) * env_exp(dur, decay, sr, 0.001)


def transient(dur, fc, decay, sr=SR, kind="lp"):
    return biquad(noise(dur, sr), fc, 0.8, kind, sr) * env_exp(dur, decay, sr, 0.0005)


def bell(freq, dur, ratio=3.5, index=2.5, decay=0.4, sr=SR):
    t = T(dur, sr)
    ie = np.exp(-t / (decay * 0.5))
    return fm(freq, ratio, index, dur, sr, ie) * env_exp(dur, decay, sr, 0.001)


def sfx_swing_heavy():
    x = whoosh(0.36, 250, 1300, 380, q=1.4, peak_at=0.5)
    low = osc(np.linspace(90, 60, int(0.36 * SR))) * np.sin(np.linspace(0, np.pi, int(0.36 * SR))) ** 2
    return mix(x, low * 0.25), 0.7


def sfx_bow():
    twang = ks_pluck(196, 0.35, SR, t60=0.25, bright=0.8) * 0.8
    w = whoosh(0.3, 1500, 4000, 2500, q=2.2, peak_at=0.25) * 0.7
    return mix(twang, delay_sig(w, 0.02)), 0.55


def sfx_magic_cast():
    d = 0.6; n = int(d * SR); t = np.linspace(0, 1, n)
    out = np.zeros(n)
    for i, base in enumerate([440, 554, 659, 880]):
        f = base * (1 + 0.5 * t) * (1 + 0.006 * np.sin(2 * np.pi * (5 + i) * t * d))
        out += osc(f, sr=SR) / (i + 1.5)
    env = np.sin(np.pi * np.clip(t * 1.1, 0, 1)) ** 1.5
    sh = svf(noise(d), 1500 + 4000 * t, 3, "bp") * 0.5
    return (out * 0.6 + sh) * env, 0.6


def sfx_fireball():
    d = 0.8; n = int(d * SR); t = np.linspace(0, 1, n)
    roar = svf(noise(d), 300 + 900 * np.sin(np.pi * t), 1.0, "lp") * (np.sin(np.pi * np.clip(t * 1.3, 0, 1)))
    crk = biquad(crackle(d, 140), 2500, 0.7, "bp") * (1 - t) * 1.5
    rumble = osc(np.full(n, 70.0)) * np.sin(np.pi * t) * 0.4
    return np.tanh(mix(roar * 1.2, crk, rumble) * 1.5), 0.8


def sfx_explosion():
    d = 1.2; n = int(d * SR); t = np.arange(n) / SR
    boom = thump(d, 110, 32, 0.35) * 1.4
    fc = 300 + 5000 * np.exp(-t / 0.12)
    body = svf(noise(d), fc, 0.8, "lp") * env_exp(d, 0.3, SR, 0.002) * 1.3
    crk = biquad(crackle(d, 90, decay=0.006), 1800, 0.7, "bp") * np.exp(-t / 0.5) * 1.2
    return np.tanh(mix(boom, body, crk) * 2.2), 1.0


def sfx_ice():
    d = 0.7
    crack = biquad(transient(d, 4000, 0.01, kind="hp"), 9000, 0.7, "lp") * 0.8
    out = crack
    for i in range(7):
        f = rng.uniform(1800, 4200)
        st = rng.uniform(0, 0.25)
        b = bell(f, d - st, ratio=rng.choice([2.76, 3.1, 5.4]), index=0.8, decay=rng.uniform(0.08, 0.2))
        out = mix(out, delay_sig(b * 0.35, st))
    shimmer = biquad(biquad(noise(d), 5000, 1.0, "hp"), 9000, 0.7, "lp") * env_exp(d, 0.12) * 0.1
    return biquad(mix(out, shimmer), 10000, 0.7, "lp"), 0.55


def sfx_lightning():
    d = 0.8; n = int(d * SR); t = np.arange(n) / SR
    gate = np.repeat(rng.random(n // 220 + 1) < 0.55, 220)[:n].astype(float)
    gate = lp1(gate, 800)
    zap = biquad(noise(d), 2500, 0.6, "hp") * gate * np.exp(-t / 0.25)
    buzz = additive_saw(60 + 40 * rng.random(n) ** 8 * 30, sr=SR, maxh=20) * gate * np.exp(-t / 0.2) * 0.5
    thunder = lp1(noise(d), 180) * 6 * env_exp(d, 0.3, SR, 0.05)
    return biquad(np.tanh(mix(zap * 0.7, buzz, thunder) * 1.8), 8000, 0.7, "lp"), 0.8


def chime(notes, step, dur, decay=0.5, ratio=2.0, index=1.2, level=0.5):
    out = np.zeros(int(dur * SR))
    for i, m in enumerate(notes):
        st = i * step
        b = bell(mtof(m), dur - st, ratio=ratio, index=index, decay=decay) * level
        out = mix(out, delay_sig(b, st))
    return out[:int(dur * SR)]


def sfx_heal():
    d = 1.0
    a = chime([72, 76, 79, 84], 0.09, d, decay=0.35, ratio=1.0, index=0.6, level=0.5)
    pad = sum(osc(mtof(m) * (1 + det), d) for m in (72, 79) for det in (-0.004, 0.004))
    pad = pad * adsr(d, 0.15, 0.2, 0.6, 0.5) * 0.18
    sh = biquad(biquad(noise(d), 5000, 1.0, "hp"), 9000, 0.7, "lp") * adsr(d, 0.3, 0.2, 0.3, 0.4) * 0.04
    return mix(a, pad, sh), 0.55


def sfx_buff():
    d = 0.6; n = int(d * SR); t = np.linspace(0, 1, n)
    f = 300 * (3 ** t)
    x = osc(f) + 0.5 * osc(f * 1.5) + 0.25 * osc(f * 2)
    env = adsr(d, 0.05, 0.1, 0.8, 0.3)
    return x * env, 0.5


def sfx_shield():
    d = 0.6; t = T(d)
    hum = fm(220, 1.5, 2.0, d, SR, np.exp(-t / 0.3)) * 0.6 + osc(110, d) * 0.4
    trem = 1 + 0.25 * np.sin(2 * np.pi * 14 * t)
    ring = bell(880, d, ratio=1.4, index=1.2, decay=0.2) * 0.3
    return mix(hum * trem * adsr(d, 0.01, 0.15, 0.5, 0.35), ring), 0.6


def growl(d, f0, f1, formants=(500, 1100), rough=30.0):
    n = int(d * SR); t = np.arange(n) / SR
    f = np.linspace(f0, f1, n) * (1 + 0.03 * np.sin(2 * np.pi * 6 * t))
    src = additive_saw(f, sr=SR, maxh=30) * (1 + 0.5 * np.sin(2 * np.pi * rough * t))
    src += lp1(noise(d), 1500) * 0.4
    out = sum(biquad(src, fm_, 3, "bp") for fm_ in formants)
    return out


def sfx_enemy_alert():
    d = 0.45
    g = growl(d, 120, 175, (450, 1000)) * adsr(d, 0.04, 0.1, 0.8, 0.2)
    return np.tanh(g * 2.5), 0.6


def sfx_enemy_attack():
    d = 0.3
    w = whoosh(d, 300, 1600, 500, q=1.4, peak_at=0.4) * 0.8
    g = growl(d, 150, 110, (500, 900)) * adsr(d, 0.02, 0.1, 0.6, 0.15) * 0.5
    return mix(w, np.tanh(g * 2)), 0.6


def sfx_player_hurt():
    d = 0.32
    g = growl(d, 210, 150, (700, 1200), rough=0) * adsr(d, 0.01, 0.08, 0.5, 0.18)
    x = mix(np.tanh(g * 1.5) * 0.8, thump(d, 170, 60, 0.05) * 0.8, transient(d, 2500, 0.01) * 0.5)
    return x, 0.75


def brass(m, dur, sr=SR, cutoff=2200, att=0.03):
    f = mtof(m)
    n = int(dur * sr)
    vib = 1 + 0.004 * np.sin(2 * np.pi * 5.5 * np.arange(n) / sr)
    x = additive_saw(f * vib, sr=sr, maxh=24) + additive_saw(f * 1.004 * vib, sr=sr, maxh=24)
    e = adsr(dur, att, 0.1, 0.75, min(0.15, dur * 0.4), sr)
    return biquad(x, cutoff, 0.7, "lp", sr) * e * 0.5


def sfx_skill_unlock():
    d = 1.0
    out = np.zeros(int(d * SR))
    for i, m in enumerate([67, 71, 74, 79]):
        out = mix(out, delay_sig(brass(m, 0.12, cutoff=2400) * 0.8, i * 0.08))
    out = mix(out, delay_sig(chime([79, 83, 86, 91], 0.04, 0.7, decay=0.3, ratio=2.0, index=1.0, level=0.45), 0.3))
    return out[:int(d * SR)], 0.7


def sfx_portal():
    d = 1.2; n = int(d * SR); t = np.arange(n) / SR
    lfo = 0.5 + 0.5 * np.sin(2 * np.pi * 3.0 * t)
    sw = svf(noise(d), 400 + 2200 * lfo, 4.0, "bp") * 0.6
    tones = sum(osc(mtof(m) * (1 + 0.01 * np.sin(2 * np.pi * (0.7 + i) * t))) for i, m in enumerate((57, 64, 69, 76)))
    tones *= 0.18
    return mix(sw, tones) * adsr(d, 0.25, 0.2, 0.8, 0.45), 0.6


def sfx_death():
    d = 1.2
    out = np.zeros(int(d * SR))
    for i, m in enumerate([62, 58, 55, 50]):
        out = mix(out, delay_sig(brass(m, 0.5, cutoff=1200) * 0.6, i * 0.22))
    out = mix(out, thump(d, 120, 40, 0.15) * 0.7)
    return out[:int(d * SR)], 0.75


def sfx_combo():
    d = 0.3
    out = np.zeros(int(d * SR))
    for i, m in enumerate([84, 88, 91]):
        out = mix(out, delay_sig(bell(mtof(m), 0.12, ratio=2.0, index=0.6, decay=0.05) * 0.6, i * 0.055))
    return out[:int(d * SR)], 0.45


def sfx_boss_roar():
    d = 1.2; n = int(d * SR); t = np.arange(n) / SR
    f = 75 + 25 * np.sin(np.pi * t / d) * (1 + 0.05 * np.sin(2 * np.pi * 7 * t))
    src = sum(additive_saw(f * r, sr=SR, maxh=30) for r in (1.0, 1.013, 0.497))
    src *= 1 + 0.6 * np.sin(2 * np.pi * 23 * t)
    src += lp1(noise(d), 1200) * 1.5
    vowel = sum(biquad(src, fr, 2.5, "bp") * g for fr, g in ((380, 1.0), (800, 0.8), (2400, 0.3)))
    vowel = np.tanh(vowel * 3.5)
    sub = osc(f * 0.5) * 0.5
    return mix(vowel, sub) * adsr(d, 0.08, 0.2, 0.85, 0.4), 1.0


# ---------------------------------------------------------------------------
# FERRAMENTAS EXTRAS (habilidades, ultimates, combo)
# ---------------------------------------------------------------------------
def tailfade(x, sec):
    """Fade-out em cosseno nos últimos 'sec' segundos (cauda limpa, sem corte)."""
    x = x.copy()
    n = min(len(x), int(sec * SR))
    if n > 1:
        x[-n:] *= 0.5 + 0.5 * np.cos(np.linspace(0, np.pi, n))
    return x


def reverb(x, size=1.0, damp=4500, wet=0.3, fb=0.80, tail=0.5, pre=0.0):
    """Reverb Schroeder simples (4 combs em paralelo + 2 allpass em série).
    Acrescenta 'tail' segundos de cauda e devolve dry + wet."""
    x = fit(x, len(x) + int(tail * SR))
    w = lp1(x, damp)
    if pre > 0:
        w = fit(delay_sig(w, pre), len(x))
    combs = [int(SR * s * size) for s in (0.0297, 0.0371, 0.0411, 0.0437)]
    acc = sum(comb_loop(w, D, fb) for D in combs) / 4
    for D in (int(SR * 0.005), int(SR * 0.0017)):
        acc = allpass(acc, D, 0.7)
    acc = hp1(acc, 120)  # cauda sem embolar os graves
    return tailfade(x * (1 - wet * 0.4) + acc * wet, min(0.25, max(0.12, tail * 0.6)))


def vdelay(x, delay):
    """Atraso variável por amostra (interpolação linear) — base de chorus/doppler."""
    n = len(x)
    idx = np.arange(n) - np.asarray(delay) * SR
    return np.interp(idx, np.arange(n), x, left=0.0, right=0.0)


def chorus(x, voices=3, depth=0.004, base=0.012, rate=0.8):
    t = np.arange(len(x)) / SR
    out = x.copy()
    for v in range(voices):
        ph = 2 * np.pi * v / voices
        d = base + depth * (0.5 + 0.5 * np.sin(2 * np.pi * rate * (1 + 0.27 * v) * t + ph))
        out += vdelay(x, d) * 0.7
    return out / (1 + 0.7 * voices) * 1.6


def drive(x, amt):
    return np.tanh(x * amt) / math.tanh(amt)


FORMANTS = {
    "a": ((800, 1.0), (1150, 0.5), (2900, 0.12)),
    "o": ((450, 1.0), (800, 0.35), (2830, 0.06)),
    "u": ((325, 1.0), (700, 0.2), (2700, 0.04)),
    "e": ((400, 1.0), (1700, 0.35), (2600, 0.1)),
}


def voice(freq, dur, vowel="a", vib=5.5, vib_amt=0.012, breath=0.08, maxh=36):
    """Voz sintética: dente-de-serra com vibrato passando por formantes."""
    n = int(dur * SR); t = np.arange(n) / SR
    f = (np.full(n, float(freq)) if np.isscalar(freq) else fit(np.asarray(freq, float), n))
    ramp = np.clip(t / 0.35, 0, 1)  # vibrato entra devagar (natural)
    f = f * (1 + vib_amt * ramp * np.sin(2 * np.pi * (vib + rng.uniform(-0.4, 0.4)) * t + rng.uniform(0, 6.28)))
    src = additive_saw(f, sr=SR, maxh=maxh) + lp1(noise(dur), 3000) * breath
    return sum(biquad(src, fr, 6.0, "bp") * g for fr, g in FORMANTS[vowel]) * 3.0


def choir(notes, dur, vowel="a", att=0.25, rel=0.5, per_note=3, det=0.006):
    """Coral: várias vozes desafinadas levemente por nota, envelope suave."""
    out = np.zeros(int(dur * SR))
    for m in notes:
        for k in range(per_note):
            dd = 1 + det * (k - (per_note - 1) / 2)
            out += voice(mtof(m) * dd, dur, vowel, vib_amt=0.009)
    return out * adsr(dur, att, 0.2, 0.85, rel) / (len(notes) * per_note) ** 0.6


def gong(freq, dur, decay=1.2, bright=1.0):
    """Gongo/sino metálico: parciais inarmônicos com batimento + ataque de batida."""
    t = T(dur)
    parts = ((1.0, 1.0, 1.0), (1.47, 0.7, 0.8), (2.09, 0.55, 0.6), (2.56, 0.4, 0.5),
             (3.07, 0.3, 0.4), (4.11, 0.22, 0.28), (5.43, 0.15, 0.2))
    out = np.zeros(len(t))
    for r, g, dk in parts:
        f = freq * r
        beat = 1 + 0.3 * np.sin(2 * np.pi * rng.uniform(0.5, 2.5) * t)
        out += np.sin(2 * np.pi * f * t + rng.uniform(0, 6.28)) * g * beat * np.exp(-t / (decay * dk / (0.6 + 0.4 * bright)))
    strike = transient(dur, 2500 * bright, 0.006) * 0.6
    return (out + strike) * env_exp(dur, 99, SR, 0.002)


def whistle(dur, f0, f1, q=12.0, tone=0.5):
    """Assobio (flecha/meteoro): seno com glide + ruído em banda estreita acompanhando."""
    n = int(dur * SR)
    t = np.linspace(0, 1, n)
    f = f0 * (f1 / f0) ** t
    s = osc(f) * tone
    nb = svf(noise(dur), f, q, "bp") * 2.0
    return s + nb


def debris(dur, density=60, fc=1500):
    """Pedrinhas/terra caindo: estalos filtrados que rareiam."""
    t = T(dur)
    return biquad(crackle(dur, density, decay=0.003), fc, 0.8, "bp") * np.exp(-t / (dur * 0.4))


def metal_ring(freq, dur, decay=0.25):
    return bell(freq, dur, ratio=1.41, index=1.6, decay=decay) + bell(freq * 2.32, dur, ratio=1.0, index=0.4, decay=decay * 0.6) * 0.4


def arc(dur, density=0.5, f=3000):
    """Arco elétrico curto: ruído cortado aleatoriamente + zumbido serrilhado."""
    n = int(dur * SR)
    blk = 90
    gate = np.repeat(rng.random(n // blk + 1) < density, blk)[:n].astype(float)
    gate = lp1(gate, 1500)
    z = biquad(noise(dur), f, 0.6, "hp") * gate
    hum = additive_saw(np.full(n, rng.uniform(90, 140)) * (1 + 0.2 * rng.random(n) ** 6), sr=SR, maxh=25) * gate * 0.5
    return (z + hum) * env_exp(dur, dur * 0.45, SR, 0.0005)


# ------------------------------------------------------------------ combo


# ------------------------------------------------------------------ guerreiro
def sfx_sk_spin():
    # golpe giratório: whoosh que dá duas voltas (filtro e volume pulsando) + "shing" de aço
    d = 0.65; n = int(d * SR); t = np.linspace(0, 1, n)
    ph = 2 * np.pi * 2.0 * t ** 0.85
    fc = 900 + 2200 * (0.5 + 0.5 * np.sin(ph - np.pi / 2))
    amp = (0.35 + 0.65 * (0.5 + 0.5 * np.sin(ph - np.pi / 2))) * np.sin(np.pi * t) ** 0.6
    w = svf(noise(d), fc, 1.8, "bp") * amp
    shing = delay_sig(metal_ring(2100, 0.4, 0.09) * 0.22, 0.12)
    return tailfade(mix(w * 1.2, shing), 0.1), 0.68


def sfx_sk_charge():
    # investida: passos acelerando + whoosh longo + pancada
    d = 0.85
    out = np.zeros(int(d * SR))
    for i, st in enumerate((0.0, 0.11, 0.2, 0.27)):
        out = mix(out, delay_sig(thump(0.12, 90, 50, 0.03) * (0.4 + 0.15 * i) + transient(0.12, 1200, 0.01) * 0.3, st))
    out = mix(out, delay_sig(whoosh(0.45, 300, 2400, 700, q=1.3, peak_at=0.7) * 1.1, 0.12))
    out = mix(out, delay_sig(np.tanh(mix(thump(0.3, 150, 45, 0.08) * 1.4, transient(0.3, 3000, 0.012) * 0.8) * 1.8), 0.55))
    return tailfade(out[:int(d * SR)], 0.2), 0.8


def sfx_sk_warcry():
    # grito de guerra: rugido grave humano com distorção + chorus (vários guerreiros) + reverb
    d = 1.1; n = int(d * SR); t = np.arange(n) / SR
    f = 105 + 35 * np.sin(np.pi * np.clip(t / 0.5, 0, 1)) ** 0.7 - 20 * np.clip((t - 0.6) / 0.5, 0, 1)
    v = voice(f, d, "a", vib=6.5, vib_amt=0.02, breath=0.4) + voice(f * 1.5, d, "o", vib=6.0, breath=0.3) * 0.4
    v = v * (1 + 0.35 * np.sin(2 * np.pi * 28 * t))  # aspereza (rasgado)
    v = drive(v * adsr(d, 0.05, 0.2, 0.85, 0.35), 4.0)
    v = chorus(v, voices=3, depth=0.006, base=0.015, rate=0.9)
    sub = osc(f * 0.5) * adsr(d, 0.05, 0.2, 0.8, 0.35) * 0.35
    return reverb(mix(v, sub), size=1.1, wet=0.25, fb=0.78, tail=0.45), 0.92


def sfx_sk_leap():
    # salto esmagador: impulso subindo -> queda -> impacto no chão com detritos
    d = 1.15
    up = whoosh(0.42, 200, 1600, 2600, q=1.5, peak_at=0.8) * 0.8
    down = delay_sig(whoosh(0.22, 2600, 1200, 300, q=1.4, peak_at=0.6) * 0.9, 0.38)
    t = T(0.75)
    boom = mix(thump(0.75, 130, 32, 0.22) * 1.6, transient(0.75, 1800, 0.03) * 1.0,
               svf(noise(0.75), 200 + 2500 * np.exp(-t / 0.06), 0.8, "lp") * env_exp(0.75, 0.15) * 1.0,
               debris(0.75, 80, 1800) * 0.6)
    out = mix(up, down, delay_sig(np.tanh(boom * 2.0), 0.4))
    return tailfade(out[:int(d * SR)], 0.3), 0.95


def sfx_sk_whirlwind():
    # redemoinho: whoosh giratório acelerando, com efeito doppler (tom de vento sobe/desce a cada volta)
    d = 1.45; n = int(d * SR); t = np.arange(n) / SR
    rot = 2 * np.pi * np.cumsum(3.0 + 4.0 * np.clip(t / 0.8, 0, 1)) / SR  # voltas acelerando
    pos = np.sin(rot)
    vel = np.cos(rot)
    env = adsr(d, 0.15, 0.2, 0.9, 0.35)
    wind = svf(noise(d), 700 * (1 + 0.45 * vel) + 800 * (0.5 + 0.5 * pos), 2.5, "bp")
    tone = osc(420 * (1 + 0.06 * vel)) * 0.12 + osc(630 * (1 + 0.06 * vel)) * 0.06
    gain = 0.45 + 0.55 * (0.5 + 0.5 * pos)  # mais forte quando "passa perto"
    blade = svf(noise(d), 3500 * (1 + 0.1 * vel), 6, "bp") * gain ** 3 * 0.5
    low = lp1(noise(d), 160) * 3.0
    return mix(wind * gain, tone * gain, blade, low * 0.4) * env, 0.75


def sfx_sk_execute():
    # executar: inspiração sombria crescente + golpe descendente + impacto brutal com sub
    d = 1.1; n = int(d * SR); t = np.arange(n) / SR
    swell = svf(noise(0.4), np.linspace(200, 1400, int(0.4 * SR)), 3, "bp") * np.linspace(0, 1, int(0.4 * SR)) ** 2 * 0.6
    sw = delay_sig(whoosh(0.25, 2500, 1500, 300, q=1.6, peak_at=0.6), 0.28)
    hd = 0.75; th = T(hd)
    hit = mix(thump(hd, 180, 30, 0.2) * 1.8, transient(hd, 4000, 0.015) * 1.0,
              biquad(noise(hd), 700, 1.2, "bp") * env_exp(hd, 0.06) * 1.0,
              metal_ring(880, hd, 0.2) * 0.3, osc(40 + 15 * np.exp(-th / 0.1)) * env_exp(hd, 0.3) * 0.6)
    out = mix(swell, sw, delay_sig(np.tanh(hit * 2.6), 0.4))
    return reverb(out[:n], size=1.0, wet=0.18, fb=0.75, tail=0.3), 1.0


# ------------------------------------------------------------------ arqueiro
def twang(f=196, d=0.35, t60=0.25, bright=0.8):
    return ks_pluck(f, d, SR, t60=t60, bright=bright)


def sfx_sk_volley():
    # rajada: 4 disparos rápidos com assobios em afinações diferentes
    d = 0.95
    out = np.zeros(int(d * SR))
    for i, (st, f, wf) in enumerate(((0.0, 196, 3200), (0.09, 220, 3600), (0.17, 185, 2900), (0.26, 208, 3900))):
        out = mix(out, delay_sig(twang(f, 0.3) * 0.7, st))
        out = mix(out, delay_sig(whistle(0.45, wf, wf * 0.7, q=10, tone=0.15) * env_exp(0.45, 0.12, SR, 0.03) * 0.5, st + 0.02))
    return out[:int(d * SR)], 0.7


def sfx_sk_pierce():
    # flecha perfurante: corda rangendo, disparo forte, assobio longo com doppler e impacto seco
    d = 1.0; n = int(d * SR)
    creak = biquad(crackle(0.25, 400, decay=0.002), 900, 3, "bp") * np.linspace(0.2, 1, int(0.25 * SR)) * 0.6
    tw = delay_sig(twang(150, 0.4, 0.3, 0.9) * 1.0, 0.24)
    wd = 0.55; tt = np.linspace(0, 1, int(wd * SR))
    wh = whistle(wd, 4200, 2600, q=14, tone=0.35) * (np.sin(np.pi * tt) ** 1.5) * 0.7
    crunch = mix(transient(0.25, 2500, 0.01) * 0.9, thump(0.25, 220, 80, 0.04) * 0.6)
    out = mix(creak, tw, delay_sig(wh, 0.26), delay_sig(crunch, 0.72))
    return out[:n], 0.75


def sfx_sk_frost_arrow():
    # flecha de gelo: disparo + rastro gelado + cristais tilintando no impacto
    d = 1.3
    tw = twang(220, 0.35) * 0.7
    trail = delay_sig(whistle(0.4, 3800, 3000, q=8, tone=0.2) * env_exp(0.4, 0.15, SR, 0.02) * 0.5, 0.02)
    sh = delay_sig(biquad(noise(0.5), 6000, 0.7, "hp") * adsr(0.5, 0.05, 0.1, 0.5, 0.3) * 0.08, 0.05)
    crack = delay_sig(biquad(transient(0.3, 6000, 0.008, kind="hp"), 10000, 0.7, "lp") * 0.9, 0.38)
    crys = np.zeros(int(0.9 * SR))
    for i in range(14):
        st = 0.0 + rng.uniform(0, 0.35) * (i / 14) ** 0.5
        f = rng.uniform(2200, 6000)
        b = bell(f, 0.9 - st, ratio=rng.choice([2.76, 3.1, 5.4]), index=0.7, decay=rng.uniform(0.08, 0.25))
        crys = mix(crys, delay_sig(b * rng.uniform(0.15, 0.35), st))
    out = mix(tw, trail, sh, crack, delay_sig(crys[:int(0.9 * SR)], 0.38))
    return reverb(out[:int(d * SR)], size=0.7, wet=0.2, fb=0.7, tail=0.25), 0.62


def sfx_sk_trap():
    # armadilha: catraca metálica armando, mola e mandíbula fechando com estalo
    d = 0.7
    out = np.zeros(int(d * SR))
    for i in range(5):
        c = metal_ring(1800 + 120 * i, 0.08, 0.015) * 0.25 + transient(0.08, 5000, 0.002) * 0.4
        out = mix(out, delay_sig(c, i * 0.045))
    spring = delay_sig(osc(np.linspace(300, 900, int(0.12 * SR))) * env_exp(0.12, 0.05) * 0.3 *
                       (1 + 0.5 * np.sin(2 * np.pi * 60 * T(0.12))), 0.24)
    snap = delay_sig(mix(transient(0.4, 3000, 0.008) * 1.2, metal_ring(950, 0.4, 0.12) * 0.45,
                         thump(0.4, 200, 70, 0.04) * 0.7), 0.34)
    out = mix(out, spring, snap)
    return out[:int(d * SR)], 0.7


def sfx_sk_roll():
    # rolamento: tecido/terra raspando + whoosh curto + dois baques suaves
    d = 0.55; t = np.linspace(0, 1, int(d * SR))
    rustle = biquad(crackle(d, 900, decay=0.0015), 2200, 0.8, "bp") * np.sin(np.pi * t) * 0.8
    w = whoosh(d, 400, 1800, 600, q=1.1, peak_at=0.4) * 0.7
    b1 = thump(0.15, 110, 60, 0.03) * 0.5 + transient(0.15, 900, 0.01) * 0.3
    out = mix(rustle, w, delay_sig(b1, 0.12), delay_sig(b1 * 0.8, 0.36))
    return out[:int(d * SR)], 0.55


def sfx_sk_arrow_rain():
    # chuva de flechas: disparo para o alto, várias flechas assobiando ao cair e cravando no chão
    d = 1.6
    out = twang(175, 0.4, 0.3, 0.9) * 0.7
    out = mix(out, delay_sig(whistle(0.35, 2500, 4500, q=8, tone=0.1) * env_exp(0.35, 0.1) * 0.4, 0.02))
    for i in range(16):
        st = 0.35 + rng.uniform(0, 0.55)
        wd = 0.35
        f0 = rng.uniform(3000, 5200)
        w = whistle(wd, f0, f0 * 0.55, q=12, tone=0.25) * np.sin(np.linspace(0, np.pi, int(wd * SR))) ** 2 * 0.22
        thk = transient(0.1, 2000, 0.006) * 0.35 + thump(0.1, 260, 120, 0.015) * 0.25
        out = mix(out, delay_sig(w, st), delay_sig(thk, st + wd - 0.02))
    return out[:int(d * SR)], 0.72


# ------------------------------------------------------------------ mago
def sfx_sk_fireball():
    # bola de fogo: ignição "fuump", rugido da chama passando e crepitar
    d = 1.0; n = int(d * SR); t = np.linspace(0, 1, n)
    ign = svf(noise(0.3), np.linspace(150, 2500, int(0.3 * SR)), 0.9, "lp") * np.linspace(0, 1, int(0.3 * SR)) ** 1.5
    roar = svf(noise(d), 400 + 1500 * np.sin(np.pi * t) ** 2, 1.2, "lp") * np.sin(np.pi * np.clip(t * 1.2 - 0.1, 0, 1)) * 1.2
    crk = biquad(crackle(d, 220, decay=0.003), 3000, 0.7, "bp") * np.sin(np.pi * t) * 1.4
    body = osc(80 + 40 * np.sin(np.pi * t)) * np.sin(np.pi * t) * 0.35
    return tailfade(np.tanh(mix(ign, roar, crk, body) * 1.6), 0.15), 0.82


def sfx_sk_heal():
    # cura: harpa em arpejo maior ascendente + brilho + pad suave com reverb
    d = 1.3
    out = np.zeros(int(d * SR))
    for i, m in enumerate((67, 71, 74, 79, 83, 86)):
        out = mix(out, delay_sig(ks_pluck(mtof(m), 0.8, SR, t60=0.9, bright=0.5) * 0.45, i * 0.06))
    pad = sum(osc(mtof(m) * (1 + det), d) for m in (67, 74, 79) for det in (-0.003, 0.003)) * adsr(d, 0.3, 0.2, 0.6, 0.6) * 0.07
    sh = biquad(noise(d), 7000, 0.7, "hp") * adsr(d, 0.4, 0.2, 0.4, 0.5) * 0.05
    return reverb(mix(out, pad, sh)[:int(d * SR)], size=1.2, wet=0.35, fb=0.8, tail=0.3), 0.58


def sfx_sk_blast():
    # explosão arcana: carga com zumbido subindo e estouro concussivo com eco cristalino
    d = 0.95; n = int(d * SR)
    cd = 0.25; tc = np.linspace(0, 1, int(cd * SR))
    charge = (osc(300 * 4 ** tc) * 0.5 + osc(450 * 4 ** tc) * 0.3) * tc ** 2 * 0.6
    bd = 0.7; tb = T(bd)
    burst = mix(thump(bd, 220, 50, 0.12) * 1.3,
                svf(noise(bd), 300 + 6000 * np.exp(-tb / 0.05), 0.9, "lp") * env_exp(bd, 0.1) * 1.2,
                fm(660, 1.5, 3, bd, SR, np.exp(-tb / 0.08)) * env_exp(bd, 0.15) * 0.3)
    out = mix(charge, delay_sig(np.tanh(burst * 2.0), cd))
    return reverb(out[:n], size=0.9, wet=0.22, fb=0.74, tail=0.3), 0.92


def sfx_sk_chain():
    # corrente elétrica: 4 arcos saltando entre alvos (cada um mais distante/fraco) + zumbido
    d = 1.05
    out = np.zeros(int(d * SR))
    for i, st in enumerate((0.0, 0.17, 0.32, 0.47)):
        a = arc(0.28 - 0.03 * i, density=0.55, f=2800 + 400 * i) * (1.0 - 0.17 * i)
        snap = transient(0.05, 7000, 0.003, kind="hp") * (1.0 - 0.15 * i)
        out = mix(out, delay_sig(mix(a, snap), st))
    buzz = additive_saw(np.full(int(d * SR), 100.0), sr=SR, maxh=30) * adsr(d, 0.01, 0.1, 0.4, 0.4) * 0.12
    return biquad(drive(mix(out, buzz), 1.6), 9000, 0.7, "lp"), 0.75


def sfx_sk_blink():
    # teleporte: "fwip" com ruído reverso (sugado) + tom sumindo e brilho na chegada
    d = 0.45; n = int(d * SR)
    rev = (whoosh(0.22, 800, 4000, 2000, q=2.0, peak_at=0.3) + bell(1800, 0.22, 2.0, 1.0, 0.06) * 0.3)[::-1] * 0.9
    fwip = osc(np.geomspace(500, 3000, int(0.12 * SR))) * np.sin(np.linspace(0, np.pi, int(0.12 * SR))) * 0.5
    spark = chime([96, 100, 103], 0.03, 0.22, decay=0.06, ratio=3.0, index=0.8, level=0.25)
    out = mix(rev, delay_sig(fwip, 0.19), delay_sig(spark, 0.23))
    return out[:n], 0.55


def sfx_sk_meteor():
    # meteoro: assobio descendo do céu (crescendo) + explosão grave com detritos
    d = 1.6; fall = 0.7
    tf = np.linspace(0, 1, int(fall * SR))
    w = whistle(fall, 2200, 260, q=5, tone=0.35) * tf ** 1.6
    roar = svf(noise(fall), 300 + 1500 * tf, 1.0, "lp") * tf ** 2 * 1.2
    bd = d - fall + 0.02; tb = T(bd)
    boom = mix(thump(bd, 120, 28, 0.35) * 1.8,
               svf(noise(bd), 250 + 5000 * np.exp(-tb / 0.1), 0.8, "lp") * env_exp(bd, 0.3) * 1.4,
               debris(bd, 70, 1500) * 1.0, osc(35, bd) * env_exp(bd, 0.4) * 0.6)
    out = mix(w, roar, delay_sig(np.tanh(boom * 2.4), fall - 0.02))
    return tailfade(out[:int(d * SR)], 0.3), 1.0


# ------------------------------------------------------------------ tank
def sfx_sk_slam():
    # esmagar com escudo/martelo: pancada grave + clangor metálico + poeira
    d = 0.85; t = T(d)
    x = mix(thump(d, 160, 35, 0.16) * 1.6, transient(d, 2000, 0.02) * 1.0,
            metal_ring(330, d, 0.25) * 0.35, metal_ring(740, d, 0.15) * 0.15,
            lp1(noise(d), 600) * env_exp(d, 0.2) * 1.5, debris(d, 60, 1300) * 0.5)
    return reverb(np.tanh(x * 2.2), size=0.9, wet=0.18, fb=0.72, tail=0.25), 0.95


def sfx_sk_fortress():
    # fortaleza: gongo metálico + zumbido sagrado (quinta justa) que sobe e brilha
    d = 1.6; t = T(d)
    g = gong(98, d, decay=1.0) * 0.8
    hum = sum(osc(mtof(m) * (1 + 0.003 * k), d) for m in (43, 50, 55) for k in (-1, 1)) * 0.12
    hum *= adsr(d, 0.35, 0.3, 0.8, 0.5) * (1 + 0.15 * np.sin(2 * np.pi * 4 * t))
    shimmer = biquad(noise(d), 6000, 0.7, "hp") * adsr(d, 0.4, 0.2, 0.5, 0.5) * 0.04
    return reverb(mix(g, hum, shimmer), size=1.3, wet=0.3, fb=0.82, tail=0.0), 0.85


def sfx_sk_bash():
    # golpe de escudo: baque seco de madeira/metal com ressonância curta
    d = 0.4
    x = mix(thump(d, 200, 70, 0.05) * 1.4, transient(d, 1500, 0.012) * 1.0,
            biquad(noise(d), 450, 4, "bp") * env_exp(d, 0.04) * 1.2,  # corpo do escudo
            metal_ring(610, d, 0.08) * 0.25)
    return np.tanh(x * 2.0), 0.88


def sfx_sk_taunt():
    # provocação: bate duas vezes no escudo + "HA!" curto
    d = 0.9
    def knock(gain):
        return mix(thump(0.3, 170, 60, 0.05) * 1.2, transient(0.3, 1400, 0.01) * 0.8,
                   metal_ring(420, 0.3, 0.12) * 0.35) * gain
    hd = 0.38; th = T(hd)
    ha = drive(voice(150 + 30 * np.exp(-th / 0.1), hd, "a", breath=0.6) * adsr(hd, 0.02, 0.1, 0.6, 0.2), 3.0) * 0.55
    out = mix(knock(0.9), delay_sig(knock(1.0), 0.18), delay_sig(chorus(ha, 2), 0.42))
    return reverb(out[:int(d * SR)], size=0.8, wet=0.15, fb=0.7, tail=0.15), 0.85


def sfx_sk_quake():
    # terremoto: ronco de terra com graves profundos, rachaduras e pedras
    d = 1.6; n = int(d * SR); t = np.arange(n) / SR
    roll = 0.6 + 0.4 * lp1(rng.uniform(-1, 1, n), 8) * 30  # ondulação irregular
    rumble = lp1(lp1(noise(d), 140), 140) * 25 * np.clip(roll, 0.2, 1.5)
    sub = (osc(38 + 4 * np.sin(2 * np.pi * 3 * t)) * 0.8 + osc(55, d) * 0.3)
    crack = np.zeros(n)
    for st in (0.0, 0.25, 0.6):
        crack = mix(crack, delay_sig(transient(0.3, 900, 0.03) * 1.2 + thump(0.3, 120, 40, 0.08), st))
    env = adsr(d, 0.02, 0.4, 0.75, 0.6)
    x = mix(rumble * env, sub * env, crack[:n], debris(d, 90, 1100) * 0.5)
    return tailfade(np.tanh(x * 1.6), 0.45), 0.85


def sfx_sk_guardian():
    # guardião: coral sintetizado (acorde maior) com sino e brilho celestial
    d = 1.6
    c = choir((55, 62, 67, 71), d - 0.2, "a", att=0.18, rel=0.7)
    b = bell(mtof(83), d, ratio=2.0, index=0.8, decay=0.5) * 0.15
    sh = biquad(noise(d), 7000, 0.7, "hp") * adsr(d, 0.4, 0.3, 0.4, 0.6) * 0.04
    return reverb(mix(c, b, sh), size=1.4, wet=0.35, fb=0.82, tail=0.0), 0.75


# ------------------------------------------------------------------ ultimates
def sfx_ult_guerreiro():
    # rugido épico (coro de guerreiros distorcido) -> impacto colossal -> fogo crepitando
    d = 2.1; n = int(d * SR)
    rd = 0.95; tr = T(rd)
    f = 95 + 40 * np.sin(np.pi * np.clip(tr / 0.6, 0, 1)) ** 0.6
    roar = sum(voice(f * r, rd, v, vib=6, vib_amt=0.025, breath=0.5) * g
               for r, v, g in ((1.0, "a", 1.0), (0.995, "o", 0.6), (1.5, "a", 0.35), (0.5, "u", 0.5)))
    roar = roar * (1 + 0.4 * np.sin(2 * np.pi * 31 * tr))
    roar = chorus(drive(roar * adsr(rd, 0.08, 0.2, 0.9, 0.2), 4.5), 4, 0.007, 0.018, 0.7)
    hd = d - 0.85; th = T(hd)
    impact = mix(thump(hd, 140, 28, 0.35) * 2.0, transient(hd, 3000, 0.03) * 1.2,
                 svf(noise(hd), 200 + 5000 * np.exp(-th / 0.08), 0.8, "lp") * env_exp(hd, 0.25) * 1.4,
                 osc(32 + 10 * np.exp(-th / 0.2)) * env_exp(hd, 0.5) * 0.7)
    tfi = np.linspace(0, 1, int(hd * SR))
    fire = (svf(noise(hd), 500 + 700 * (1 - tfi), 1.0, "lp") * 1.0 +
            biquad(crackle(hd, 260, decay=0.003), 2800, 0.7, "bp") * 1.6) * np.clip(tfi * 6, 0, 1) * (1 - tfi) ** 1.2
    out = mix(roar * 0.9, delay_sig(np.tanh(impact * 2.3), 0.82), delay_sig(fire * 0.8, 0.85))
    return reverb(out[:n], size=1.2, wet=0.25, fb=0.8, tail=0.0), 1.0


def sfx_ult_arqueiro():
    # corda tensionando (rangido subindo) -> soltura -> centenas de flechas assobiando em chuva
    d = 2.5; n = int(d * SR)
    cd = 0.6; tc = np.linspace(0, 1, int(cd * SR))
    creak = biquad(crackle(cd, 600, decay=0.002), 900, 3, "bp") * tc * 0.7
    tension = osc(110 * 2 ** tc) * tc ** 2 * 0.12 * (1 + 0.3 * np.sin(2 * np.pi * 30 * T(cd)))
    rel = mix(twang(130, 0.6, 0.5, 0.95) * 1.0, twang(196, 0.5, 0.4, 0.9) * 0.6)
    out = mix(creak, tension, delay_sig(rel, cd))
    out = mix(out, delay_sig(whoosh(0.5, 800, 5000, 3000, q=1.5, peak_at=0.3) * 0.6, cd))
    # cortina de flechas: ~240 assobios curtos (seno com glide doppler) + chiado em massa
    rain = np.zeros(n)
    for _ in range(240):
        st = cd + 0.35 + rng.beta(2, 2.5) * 1.3
        wd = rng.uniform(0.18, 0.4); m = int(wd * SR)
        f0 = rng.uniform(2500, 6000)
        f = f0 * np.geomspace(1.0, rng.uniform(0.5, 0.75), m)
        s = osc(f) * np.sin(np.linspace(0, np.pi, m)) ** 2 * rng.uniform(0.04, 0.1)
        i0 = int(st * SR)
        if i0 + m < n:
            rain[i0:i0 + m] += s
            k = int(0.06 * SR)
            if i0 + m + k < n and rng.random() < 0.6:  # cravando no chão
                rain[i0 + m:i0 + m + k] += transient(0.06, 1800, 0.005) * rng.uniform(0.05, 0.15)
    hiss = svf(noise(d), 4500, 1.2, "bp") * 0.6
    hiss_env = np.clip((np.arange(n) / SR - cd - 0.3) / 0.4, 0, 1) * np.clip((2.3 - np.arange(n) / SR) / 0.6, 0, 1)
    out = mix(out, rain, hiss * hiss_env)
    return reverb(out[:n], size=1.0, wet=0.2, fb=0.75, tail=0.0), 0.95


def sfx_ult_mago():
    # vento sombrio subindo + coro em menor + explosão colossal
    d = 3.0; n = int(d * SR)
    wd = 1.8; tw = np.linspace(0, 1, int(wd * SR))
    lfo = np.sin(2 * np.pi * np.cumsum(1.5 + 5 * tw) / SR)
    wind = svf(noise(wd), 200 + 1800 * tw ** 1.5 + 300 * lfo, 3.0, "bp") * tw ** 1.3 * 1.2
    drone = (additive_saw(np.full(int(wd * SR), mtof(33)) * (1 + 0.02 * tw), sr=SR, maxh=30) * 0.5)
    drone = svf(drone, 300 + 1500 * tw, 1.5, "lp") * tw ** 1.5 * 0.6
    cd = 1.7
    ch = choir((45, 52, 57, 60, 64), cd, "o", att=0.9, rel=0.15) * 1.3
    bd = d - 1.75; tb = T(bd)
    boom = mix(thump(bd, 110, 24, 0.5) * 2.2,
               svf(noise(bd), 200 + 7000 * np.exp(-tb / 0.12), 0.8, "lp") * env_exp(bd, 0.45) * 1.6,
               osc(30 + 15 * np.exp(-tb / 0.2)) * env_exp(bd, 0.6) * 0.9,
               debris(bd, 70, 1600) * 0.8, gong(65, bd, 0.7) * 0.25)
    out = mix(wind, drone, delay_sig(ch, 0.1), delay_sig(np.tanh(boom * 2.6), 1.75))
    return reverb(out[:n], size=1.5, wet=0.3, fb=0.83, tail=0.0), 1.0


def sfx_ult_tank():
    # sino/gongo sagrado + coro maior + escudo ressoando (zumbido com batimento)
    d = 2.5; n = int(d * SR); t = np.arange(n) / SR
    g = gong(82, d, decay=1.6) * 0.8 + bell(mtof(76), d, ratio=2.0, index=1.0, decay=0.8) * 0.25
    ch = delay_sig(choir((52, 59, 64, 68, 71), 2.1, "a", att=0.4, rel=0.6), 0.25)
    sh = fm(196, 1.0, 1.2, d, SR) * (1 + 0.3 * np.sin(2 * np.pi * 6 * t)) * adsr(d, 0.5, 0.4, 0.7, 0.8) * 0.18
    sub = osc(41.2, d) * adsr(d, 0.05, 0.6, 0.4, 0.8) * 0.35
    out = mix(g, ch, sh, sub)
    return reverb(out[:n], size=1.5, wet=0.32, fb=0.83, tail=0.0), 0.95


def sfx_ult_ready():
    # ultimate pronta: sino mágico ascendente curto com brilho
    d = 0.8
    c = chime([79, 84, 88, 91, 96], 0.045, d - 0.15, decay=0.25, ratio=2.0, index=0.9, level=0.45)
    up = osc(np.geomspace(600, 2400, int(0.25 * SR))) * np.sin(np.linspace(0, np.pi, int(0.25 * SR))) * 0.15
    sh = biquad(noise(0.6), 8000, 0.7, "hp") * adsr(0.6, 0.1, 0.1, 0.3, 0.35) * 0.05
    return reverb(mix(c, up, sh), size=1.0, wet=0.3, fb=0.75, tail=0.15), 0.6


SKILL_IDS = ["spin", "charge", "warcry", "leap", "whirlwind", "execute",
             "volley", "pierce", "frost_arrow", "trap", "roll", "arrow_rain",
             "fireball", "heal", "blast", "chain", "blink", "meteor",
             "slam", "fortress", "bash", "taunt", "quake", "guardian"]
GROUPS = {
    "skills": ["sk_" + s for s in SKILL_IDS],
    "ults": ["ult_guerreiro", "ult_arqueiro", "ult_mago", "ult_tank", "ult_ready"],
    "combo": ["swing2", "swing3", "hit_heavy"],
}


EFFECTS = [
    "swing", "swing_heavy", "hit", "hit_crit", "bow", "magic_cast", "fireball", "explosion",
    "ice", "lightning", "heal", "buff", "shield", "dash", "step", "enemy_hit", "enemy_die",
    "enemy_alert", "enemy_attack", "player_hurt", "parry", "block", "coin", "chest_open",
    "item_rare", "item_legendary", "levelup", "skill_unlock", "ui_click", "ui_open", "ui_close",
    "ui_error", "portal", "death", "combo", "boss_roar", "footstep_stone",
] + GROUPS["combo"] + GROUPS["skills"] + GROUPS["ults"] + [
    # novos (v2)
    "goblin_laugh", "goblin_coins", "barrel_break", "barrel_explode", "charge_loop", "charge_release",
    "combo_frenzy", "lockon", "quest_complete", "achievement", "grade_s", "page_turn", "boss_phase",
]


# ---------------------------------------------------------------------------
# MÚSICA (22050 Hz, loops perfeitos)
# ---------------------------------------------------------------------------
class Loop:
    """Buffer de loop: tudo que passa do fim volta para o começo (sem clique)."""

    def __init__(self, bpm, bars, beats=4, sr=MSR):
        self.sr = sr
        self.beat = 60.0 / bpm
        self.bar_samples = int(round(self.beat * beats * sr))
        self.N = self.bar_samples * bars
        self.beats = beats
        self.bars = bars
        self.bus = {}

    def at(self, bar, beat=0.0):
        return int(round(bar * self.bar_samples + beat * self.beat * self.sr))

    def add(self, bus, sig, start):
        buf = self.bus.setdefault(bus, np.zeros(self.N))
        start %= self.N
        i = 0
        while i < len(sig):
            p = (start + i) % self.N
            k = min(len(sig) - i, self.N - p)
            buf[p:p + k] += sig[i:i + k]
            i += k

    def get(self, bus):
        return self.bus.get(bus, np.zeros(self.N))


def comb_loop(x, D, g):
    y = x.copy()
    s = D
    while s < len(y):
        e = min(s + D, len(y))
        y[s:e] += g * y[s - D:e - D]
        s = e
    return y


def allpass(x, D, g):
    # y[n] = -g x[n] + x[n-D] + g y[n-D]
    y = -g * x
    y[D:] += x[:-D]
    s = D
    while s < len(y):
        e = min(s + D, len(y))
        y[s:e] += g * y[s - D:e - D]
        s = e
    return y


def reverb_loop(x, sr=MSR, size=1.0, damp=3500, wet=0.3):
    """Reverb Schroeder aplicado ao loop em 3 cópias; usa a do meio (estado
    estacionário) para a cauda emendar perfeitamente."""
    N = len(x)
    xx = np.concatenate([x, x, x])
    xx = lp1(xx, damp, sr)
    combs = [int(sr * s * size) for s in (0.0297, 0.0371, 0.0411, 0.0437)]
    acc = sum(comb_loop(xx, D, 0.80) for D in combs) / 4
    for D in (int(sr * 0.005), int(sr * 0.0017)):
        acc = allpass(acc, D, 0.7)
    return x * (1 - wet * 0.5) + acc[N:2 * N] * wet


def lowpass_loop(x, fc, sr=MSR, kind="lp", q=0.707):
    """Filtro aplicado de forma circular (3 cópias) para não quebrar o loop."""
    N = len(x)
    y = biquad(np.concatenate([x, x, x]), fc, q, kind, sr)
    return y[N:2 * N]


def pad_note(m, dur, sr=MSR, cutoff=1400, att=0.6, rel=1.0, det=0.005, voices=3):
    n = int((dur + rel) * sr)
    f = mtof(m)
    x = np.zeros(n)
    for v in range(voices):
        dd = (v - (voices - 1) / 2) * det
        x += additive_saw(np.full(n, f * (1 + dd)), sr=sr, maxh=16, bright=0.7)
    x = biquad(x, cutoff, 0.7, "lp", sr) / voices
    e = adsr(dur + rel, att, 0.3, 0.8, rel, sr)
    return x * e


def sine_note(m, dur, sr=MSR, att=0.01, rel=0.2, harm=0.3):
    n = int((dur + rel) * sr)
    f = mtof(m)
    x = osc(np.full(n, f), sr=sr) + harm * osc(np.full(n, 2 * f), sr=sr)
    return x * adsr(dur + rel, att, 0.1, 0.85, rel, sr)


def kick(sr=MSR, f0=140, f1=45, decay=0.18, dur=0.4):
    n = int(dur * sr); t = np.arange(n) / sr
    f = f1 + (f0 - f1) * np.exp(-t / 0.025)
    return osc(f, sr=sr) * env_exp(dur, decay, sr, 0.001)


def tom(sr=MSR, f=100, decay=0.25, dur=0.5, nz=0.3):
    n = int(dur * sr); t = np.arange(n) / sr
    ff = f * (1 + 0.5 * np.exp(-t / 0.03))
    x = osc(ff, sr=sr) + nz * lp1(rng.uniform(-1, 1, n), 2000, sr) * np.exp(-t / 0.03)
    return x * env_exp(dur, decay, sr, 0.001)


def snare(sr=MSR, dur=0.25, tone=190):
    t = T(dur, sr)
    nz = biquad(rng.uniform(-1, 1, len(t)), 3000, 0.7, "hp", sr) * np.exp(-t / 0.06)
    tn = osc(np.full(len(t), tone), sr=sr) * np.exp(-t / 0.04)
    return nz * 0.8 + tn * 0.5


def hat(sr=MSR, dur=0.06, decay=0.015):
    t = T(dur, sr)
    return biquad(rng.uniform(-1, 1, len(t)), 7000, 0.7, "hp", sr) * np.exp(-t / decay)


def tambourine(sr=MSR, dur=0.15):
    t = T(dur, sr)
    nz = biquad(rng.uniform(-1, 1, len(t)), 6500, 1.5, "bp", sr)
    jing = sum(np.sin(2 * np.pi * f * t) for f in (5100, 6800, 8300)) * 0.15 if sr > 20000 else 0
    return (nz + jing) * np.exp(-t / 0.045)


def finalize_music(L, buses, out_name):
    x = sum(L.get(b) * g for b, g in buses)
    x = np.tanh(x * 1.2) / np.tanh(1.2)  # leve compressão suave
    x = x - np.mean(x)
    # pico no máximo -1 dBFS e loudness parecida entre as faixas (RMS alvo ~ -15 dB)
    g_peak = 10 ** (-1 / 20) / (np.max(np.abs(x)) + 1e-9)
    g_rms = 10 ** (-15 / 20) / (np.sqrt(np.mean(x ** 2)) + 1e-9)
    x = x * min(g_peak, g_rms)
    # sem fade: o loop já é contínuo (início e fim emendam)
    return write_wav(out_name, x, MSR)


def chord_tones(root, quality):
    third = 3 if quality == "m" else 4
    return [root, root + third, root + 7]


# ===========================================================================
# KIT DE SÍNTESE v2 — timbres mais "orgânicos" e menos estridentes
#   - Karplus-Strong sobreamostrado (afinação precisa, filtro de perda de 3 taps)
#   - reverb por convolução com ruído decaindo (graves decaem mais devagar)
#   - corte de agudos (~7–8 kHz), compressão leve e loops 100% circulares
# ===========================================================================
def lp(x, fc, sr=SR, q=0.707):
    return biquad(x, fc, q, "lp", sr)


def lp2(x, fc, sr=SR):
    """Passa-baixa de 4ª ordem (dois biquads) — tira a aspereza de cima."""
    return biquad(biquad(x, fc, 0.54, "lp", sr), fc, 1.31, "lp", sr)


def hp(x, fc, sr=SR, q=0.707):
    return biquad(x, fc, q, "hp", sr)


def bp(x, fc, q=1.0, sr=SR):
    return biquad(x, fc, q, "bp", sr)


def circ(x, fn):
    """Aplica 'fn' de forma circular (3 cópias, usa a do meio) — não quebra o loop."""
    N = len(x)
    return fn(np.concatenate([x, x, x]))[N:2 * N]


def fftconv(a, b):
    n = len(a) + len(b) - 1
    N = 1 << (n - 1).bit_length()
    return np.fft.irfft(np.fft.rfft(a, N) * np.fft.rfft(b, N), N)[:n]


_IR = {}


def make_ir(t60=1.5, sr=MSR, damp=5000, pre=0.012, seed=21):
    """Resposta ao impulso sintética: ruído com decaimento exponencial, agudos
    morrendo antes dos graves, reflexões iniciais e energia normalizada (=1)."""
    key = (round(t60, 3), sr, damp, pre, seed)
    if key in _IR:
        return _IR[key]
    r = np.random.default_rng(seed)
    n = int(t60 * 1.1 * sr)
    t = np.arange(n) / sr
    nz = r.standard_normal(n)
    low = lp1(lp1(nz, 650, sr), 650, sr)
    full = lp1(lp1(nz, damp, sr), damp, sr)
    hi = full - low
    ir = low * np.exp(-6.91 * t / t60) * 1.4 + hi * np.exp(-6.91 * t / (t60 * 0.5))
    ir = hp1(ir, 110, sr)
    na = int(0.025 * sr)
    ir[:na] *= np.linspace(0, 1, na) ** 2           # ataque difuso (sem "estalo")
    ir /= np.sqrt(np.sum(ir ** 2)) + 1e-12
    for k in range(6):                              # reflexões iniciais
        d = int(r.uniform(0.004, 0.03) * sr)
        ir[d] += r.uniform(0.15, 0.3) * (1 - k / 8) * r.choice([-1, 1])
    ir = np.concatenate([np.zeros(int(pre * sr)), ir])
    ir /= np.sqrt(np.sum(ir ** 2)) + 1e-12
    _IR[key] = ir
    return ir


def rev_loop(x, ir):
    """Convolução CIRCULAR (FFT do tamanho do loop): a cauda volta ao início."""
    N = len(x)
    h = np.zeros(N)
    for i in range(0, len(ir), N):
        seg = ir[i:i + N]
        h[:len(seg)] += seg
    return np.fft.irfft(np.fft.rfft(x) * np.fft.rfft(h), N)


def room(x, t60=0.5, wet=0.12, damp=6000, sr=SR):
    """Ambiência para efeitos (convolução linear; acrescenta a cauda)."""
    ir = make_ir(t60, sr, damp, pre=0.006, seed=33)
    y = fftconv(x, ir)
    out = fit(x, len(y)) + y * wet
    n = min(len(out), int(min(0.35, t60 * 0.5) * sr))
    out[-n:] *= 0.5 + 0.5 * np.cos(np.linspace(0, np.pi, n))
    return out


def buf(d, sr=SR):
    return np.zeros(int(d * sr))


def put(b, sig, t, g=1.0, sr=SR):
    """Soma 'sig' no buffer 'b' a partir de t segundos (corta o que passar)."""
    i = int(t * sr)
    k = min(len(sig), len(b) - i)
    if k > 0:
        b[i:i + k] += sig[:k] * g
    return b


def modal(freqs, amps, decays, dur, sr=SR, att=0.0006):
    """Síntese modal: soma de senos amortecidos (madeira, pedra, metal, moedas)."""
    t = T(dur, sr)
    out = np.zeros(len(t))
    for f, a, d in zip(freqs, amps, decays):
        if f < sr * 0.45:
            out += a * np.sin(2 * np.pi * f * t + rng.uniform(0, 6.28)) * np.exp(-t / d)
    na = max(1, int(att * sr))
    out[:na] *= np.linspace(0, 1, na)
    return out


def wood(f0, dur=0.12, damp=1.0, sr=SR):
    return modal([f0, f0 * 2.32, f0 * 4.25, f0 * 6.63], [1, 0.45, 0.22, 0.1],
                 [0.035 * damp, 0.02 * damp, 0.012 * damp, 0.007 * damp], dur, sr)


def coin_clink(f0, dur=0.4, dk=1.0, sr=SR):
    return modal([f0, f0 * 1.53, f0 * 2.08, f0 * 2.79, f0 * 3.6], [1, 0.6, 0.4, 0.25, 0.12],
                 [0.22 * dk, 0.14 * dk, 0.09 * dk, 0.05 * dk, 0.03 * dk], dur, sr)


def ks2(freq, dur, sr=MSR, t60=1.2, bright=0.5, os=4, pick=0.13):
    """Karplus-Strong sobreamostrado (os x) com filtro de perda [.25 .5 .25]:
    afinação precisa nos agudos e harmônicos altos morrendo rápido (som macio)."""
    srh = sr * os
    P = max(3, int(round(srh / freq - 1.0)))
    n = int(dur * srh) + P + 3
    y = np.zeros(n)
    exc = rng.uniform(-1, 1, P + 2)
    fc = 900 + bright * 6500
    exc = lp1(lp1(exc, fc, srh), fc, srh)
    exc = exc - np.roll(exc, max(1, int(P * pick)))   # posição da palheta
    y[:P + 2] = exc / (np.max(np.abs(exc)) + 1e-9)
    d = 10 ** (-3.0 / (t60 * freq))
    s = P + 2
    while s < n:
        e = min(s + P, n)
        y[s:e] = d * (0.25 * y[s - P:e - P] + 0.5 * y[s - P - 1:e - P - 1] + 0.25 * y[s - P - 2:e - P - 2])
        s = e
    y = y[:int(dur * srh)]
    y = lfilter(*_lp_coef(0.42 * sr, srh), y)
    y = lfilter(*_lp_coef(0.42 * sr, srh), y)[::os]
    return fades(fit(y, int(dur * sr)), 0.0015, 0.03, sr)


def _lp_coef(fc, sr, q=0.707):
    w = 2 * math.pi * fc / sr
    cw, sw = math.cos(w), math.sin(w)
    al = sw / (2 * q)
    return [(1 - cw) / 2, 1 - cw, (1 - cw) / 2], [1 + al, -2 * cw, 1 - al]


# ------------------------------------------------------------- instrumentos
def i_lute(m, dur, g=1.0, bright=0.5, t60=1.1, sr=MSR):
    x = ks2(mtof(m), dur, sr, t60=t60, bright=bright)
    x = x + bp(x, 240, 1.4, sr) * 0.6 + bp(x, 520, 2.0, sr) * 0.3   # tampo de madeira
    return lp(x, 4200, sr) * g


def i_harp(m, dur, g=1.0, sr=MSR, t60=None):
    f = mtof(m)
    x = ks2(f, dur, sr, t60=t60 or (2.6 if f < 500 else 1.7), bright=0.4, pick=0.5)
    t = T(dur, sr)
    x = fit(x, len(t)) + 0.3 * np.sin(2 * np.pi * f * t) * np.exp(-t / 0.9) * np.clip(t / 0.004, 0, 1)
    return lp(x, 5000, sr) * g


def i_oud(m, dur, g=1.0, sr=MSR):
    x = ks2(mtof(m), dur, sr, t60=0.8, bright=0.7, pick=0.2)
    x = x + bp(x, 300, 1.5, sr) * 0.7
    return lp(x, 3800, sr) * g


def i_pizz(m, dur, g=1.0, sr=MSR):
    x = ks2(mtof(m), dur, sr, t60=0.45, bright=0.35, pick=0.3)
    x = x + bp(x, 350, 1.2, sr) * 0.5
    return lp(x, 3000, sr) * g


def i_flute(m, dur, g=1.0, rel=0.12, att=0.06, breath=0.1, vib=0.006, sr=MSR, h2=0.22):
    n = int((dur + rel) * sr); t = np.arange(n) / sr
    f0 = mtof(m)
    f = f0 * (1 + vib * np.clip((t - 0.18) / 0.35, 0, 1) * np.sin(2 * np.pi * 5.1 * t + rng.uniform(0, 6.28)))
    ph = 2 * np.pi * np.cumsum(f) / sr
    x = np.sin(ph) + h2 * np.sin(2 * ph) + 0.06 * np.sin(3 * ph)
    nz = rng.uniform(-1, 1, n)
    br = bp(nz, min(f0 * 2, 4000), 2.0, sr) * breath * 2 + lp(nz, 5000, sr) * breath * 0.25
    chiff = bp(nz, 2500, 1, sr) * np.exp(-t / 0.025) * 0.2
    env = adsr(dur + rel, att, 0.1, 0.85, rel, sr)
    return ((x + br) * env + chiff) * g


def i_bow(m, dur, g=1.0, att=0.12, rel=0.25, bright=0.55, vib=0.006, sr=MSR, voices=2, cutoff=None):
    """Cordas friccionadas: 2 serras detunadas + vibrato tardio + formantes do corpo."""
    n = int((dur + rel) * sr); t = np.arange(n) / sr
    f0 = mtof(m)
    out = np.zeros(n)
    for v in range(voices):
        dd = 1 + (v - (voices - 1) / 2) * 0.004
        fv = f0 * dd * (1 + vib * np.clip((t - 0.2) / 0.4, 0, 1) * np.sin(2 * np.pi * (5.3 + 0.4 * v) * t + v))
        out += additive_saw(fv, sr=sr, maxh=max(4, int(min(30, 4500 / f0))), bright=bright)
    out /= voices
    c = cutoff or min(3200, f0 * 7)
    out = lp(out, c, sr) + bp(out, 450, 1.5, sr) * 0.35 + bp(out, 1300, 2, sr) * 0.15
    return out * adsr(dur + rel, att, 0.15, 0.85, rel, sr) * g


def i_pad(notes, dur, g=1.0, att=0.8, rel=1.2, cutoff=1400, det=0.006, voices=3, sr=MSR, bright=0.6):
    """Pad: vários osciladores detunados por nota + passa-baixa."""
    n = int((dur + rel) * sr)
    out = np.zeros(n)
    for m in notes:
        f = mtof(m)
        for v in range(voices):
            dd = 1 + (v - (voices - 1) / 2) * det
            ph0 = rng.uniform(0, 6.28)
            ph = 2 * np.pi * f * dd * np.arange(n) / sr + ph0
            mh = max(3, int(min(24, cutoff * 2.2 / f)))
            for k in range(1, mh + 1):
                if k * f > sr * 0.45:
                    break
                out += np.sin(k * ph) / (k ** (2.0 - bright))
    out = lp(out, cutoff, sr) / max(1, len(notes) * voices) ** 0.5 * 0.6
    return out * adsr(dur + rel, att, 0.3, 0.85, rel, sr) * g


def mvoice(f, dur, vowel="o", sr=MSR, vib_amt=0.008, breath=0.06):
    n = int(dur * sr); t = np.arange(n) / sr
    ff = np.full(n, float(f)) * (1 + vib_amt * np.clip(t / 0.4, 0, 1) *
                                 np.sin(2 * np.pi * (5.2 + rng.uniform(-0.4, 0.4)) * t + rng.uniform(0, 6.28)))
    src = additive_saw(ff, sr=sr, maxh=int(min(36, 4000 / f))) + lp1(rng.uniform(-1, 1, n), 2500, sr) * breath
    return sum(bp(src, fr, 5.0, sr) * gg for fr, gg in FORMANTS[vowel]) * 3.0


def i_choir(notes, dur, g=1.0, vowel="o", att=0.6, rel=1.0, per=3, sr=MSR):
    n = int((dur + rel) * sr)
    out = np.zeros(n)
    for m in notes:
        for k in range(per):
            out += mvoice(mtof(m) * (1 + 0.005 * (k - (per - 1) / 2)), dur + rel, vowel, sr)
    out = lp(out, 3500, sr) / (len(notes) * per) ** 0.6
    return out * adsr(dur + rel, att, 0.2, 0.85, rel, sr) * g


def i_horn(m, dur, g=1.0, att=0.07, cutoff=1300, sr=MSR, rel=0.15):
    """Trompa: serras detunadas cujo filtro abre com o volume (metal macio)."""
    f = mtof(m); n = int((dur + rel) * sr); t = np.arange(n) / sr
    vib = 1 + 0.004 * np.clip((t - 0.25) / 0.3, 0, 1) * np.sin(2 * np.pi * 5 * t)
    mh = int(max(4, min(24, 5000 / f)))
    x = additive_saw(f * vib, sr=sr, maxh=mh, bright=0.6) + additive_saw(f * 1.003 * vib, sr=sr, maxh=mh, bright=0.6)
    e = adsr(dur + rel, att, 0.15, 0.8, rel, sr)
    dark = lp(x, cutoff * 0.45, sr); brt = lp(x, cutoff, sr)
    y = dark + (brt - dark) * e + np.sin(2 * np.pi * np.cumsum(f * vib) / sr) * 0.3
    return y * e * 0.5 * g


def i_celesta(m, dur, g=1.0, sr=MSR, decay=0.9):
    f = mtof(m); t = T(dur, sr)
    modf = f * 4 if f * 4 < sr * 0.45 else f * 2
    x = np.sin(2 * np.pi * f * t + 0.6 * np.exp(-t / 0.08) * np.sin(2 * np.pi * modf * t)) * np.exp(-t / decay)
    if 2 * f < sr * 0.45:
        x += 0.22 * np.sin(2 * np.pi * 2 * f * t) * np.exp(-t / (decay * 0.35))
    na = int(0.002 * sr); x[:na] *= np.linspace(0, 1, na)
    return lp(x, 6500, sr) * g


def i_bell(freq, dur, g=1.0, sr=MSR, decay=2.5, cutoff=2500):
    """Sino distante: parciais inarmônicos com batimento, abafado."""
    t = T(dur, sr)
    out = np.zeros(len(t))
    for r, a, dk in ((0.5, 0.6, 1.4), (1.0, 1.0, 1.0), (1.19, 0.5, 0.7), (1.5, 0.4, 0.6),
                     (2.0, 0.35, 0.5), (2.52, 0.2, 0.35), (3.01, 0.12, 0.25)):
        if freq * r < sr * 0.45:
            beat = 1 + 0.2 * np.sin(2 * np.pi * rng.uniform(0.3, 1.5) * t)
            out += a * np.sin(2 * np.pi * freq * r * t + rng.uniform(0, 6.28)) * beat * np.exp(-t / (decay * dk))
    na = int(0.003 * sr); out[:na] *= np.linspace(0, 1, na)
    return lp(out, cutoff, sr) * g


# ------------------------------------------------------------- percussão (MSR)
def p_taiko(f=62, g=1.0, decay=0.45, sr=MSR, cutoff=2500):
    dur = decay * 3.5; n = int(dur * sr); t = np.arange(n) / sr
    ff = f * (1 + 0.6 * np.exp(-t / 0.02))
    body = np.sin(2 * np.pi * np.cumsum(ff) / sr) * np.exp(-t / decay)
    skin = lp(rng.uniform(-1, 1, n), 1200, sr) * np.exp(-t / 0.03) * 0.5
    x = body + skin
    x[:int(0.001 * sr)] *= np.linspace(0, 1, int(0.001 * sr))
    return lp(x, cutoff, sr) * g


def p_kick(g=1.0, sr=MSR, f0=130, f1=46, decay=0.16):
    return lp(kick(sr, f0, f1, decay, decay * 3), 3000, sr) * g


def p_snare(g=1.0, sr=MSR, tone=185, decay=0.08):
    dur = 0.35; t = T(dur, sr)
    nz = bp(rng.uniform(-1, 1, len(t)), 3200, 0.6, sr) * np.exp(-t / decay)
    tn = np.sin(2 * np.pi * tone * t) * np.exp(-t / 0.05)
    return lp(nz * 0.8 + tn * 0.6, 7000, sr) * g


def p_shaker(g=1.0, sr=MSR, dur=0.1):
    t = T(dur, sr)
    e = np.clip(t / 0.012, 0, 1) * np.exp(-np.clip(t - 0.012, 0, None) / 0.03)
    return lp(bp(rng.uniform(-1, 1, len(t)), 5000, 0.8, sr), 7500, sr) * e * g


def p_hat(g=1.0, sr=MSR):
    t = T(0.08, sr)
    return lp(bp(rng.uniform(-1, 1, len(t)), 6500, 0.9, sr), 8000, sr) * np.exp(-t / 0.018) * g


def p_tek(g=1.0, sr=MSR):
    t = T(0.09, sr)
    x = bp(rng.uniform(-1, 1, len(t)), 2600, 1.4, sr) * np.exp(-t / 0.018) + np.sin(2 * np.pi * 720 * t) * np.exp(-t / 0.012) * 0.4
    return x * g


def p_frame(g=1.0, sr=MSR, f=115):
    return lp(tom(sr, f=f, decay=0.22, dur=0.6, nz=0.25), 2500, sr) * g


def p_cym(g=1.0, sr=MSR, dur=1.6):
    t = T(dur, sr)
    nz = rng.uniform(-1, 1, len(t))
    return lp2(hp(nz, 3000, sr), 7500, sr) * np.exp(-t / 0.55) * np.clip(t / 0.003, 0, 1) * g


def p_swell(dur, g=1.0, sr=MSR):
    """Prato ao contrário (crescendo que leva para o próximo compasso)."""
    t = T(dur, sr)
    nz = rng.uniform(-1, 1, len(t))
    x = lp2(hp(nz, 2500, sr), 7000, sr) * (t / dur) ** 3
    x[-int(0.01 * sr):] *= np.linspace(1, 0, int(0.01 * sr))
    return x * g


# ------------------------------------------------------------- harmonia
PC = {"C": 0, "D": 2, "E": 4, "F": 5, "G": 7, "A": 9, "B": 11}
QUAL = {"M": (0, 4, 7), "m": (0, 3, 7), "7": (0, 4, 7, 10), "m7": (0, 3, 7, 10), "maj7": (0, 4, 7, 11),
        "sus4": (0, 5, 7), "sus2": (0, 2, 7), "dim": (0, 3, 6), "add9": (0, 4, 7, 14), "madd9": (0, 3, 7, 14),
        "5": (0, 7), "m7b5": (0, 3, 6, 10), "6": (0, 4, 7, 9)}


def CH(name):
    """'Dm', 'Bb', 'Asus4', 'Cmaj7'... -> notas MIDI com a fundamental entre 36 e 47."""
    r = PC[name[0]]; i = 1
    if len(name) > 1 and name[1] in "b#":
        r += -1 if name[1] == "b" else 1; i = 2
    q = name[i:] or "M"
    root = 36 + (r % 12)
    return [root + k for k in QUAL[q]]


def vchord(chord, lo, hi=None):
    """Distribui as classes de altura do acorde dentro de [lo, lo+12)."""
    out = sorted({lo + ((c - lo) % 12) for c in chord})
    return out


def bass_of(chord):
    r = chord[0]
    return r if r <= 43 else r - 12


def scale_notes(tonic, steps, lo, hi):
    out = []
    for m in range(lo, hi + 1):
        if (m - tonic) % 12 in steps:
            out.append(m)
    return out


MAJOR = (0, 2, 4, 5, 7, 9, 11); MINOR = (0, 2, 3, 5, 7, 8, 10); HARM = (0, 2, 3, 5, 7, 8, 11)
DORIAN = (0, 2, 3, 5, 7, 9, 10); MIXO = (0, 2, 4, 5, 7, 9, 10); PHRYG = (0, 1, 3, 5, 7, 8, 10)
HIJAZ = (0, 1, 4, 5, 7, 8, 10)


def gen_melody(chords, scale, seed, patterns, strong=(0.0, 2.0), cadence=(2, 2), start=None, leaps=(-2, -1, -1, 1, 1, 2)):
    """Melodia simples: tempos fortes caem em notas do acorde, fracos andam por grau."""
    r = np.random.default_rng(seed)
    idx = start if start is not None else len(scale) // 2
    out = []
    for b, ch in enumerate(chords):
        pcs = {c % 12 for c in ch}
        pat = list(cadence) if b == len(chords) - 1 else patterns[r.integers(len(patterns))]
        beat = 0.0
        for j, d in enumerate(pat):
            if beat in strong or j == len(pat) - 1:
                cands = [i for i, s in enumerate(scale) if s % 12 in pcs] or list(range(len(scale)))
                idx = min(cands, key=lambda i: abs(i - idx) + r.random() * 1.5)
            else:
                idx = int(np.clip(idx + r.choice(leaps), 0, len(scale) - 1))
            out.append((b, beat, d, scale[idx]))
            beat += d
    return out


def loop_noise(N, sr=MSR):
    return rng.uniform(-1, 1, N)


def lfo_loop(N, cycles, phase=0.0):
    """LFO com número inteiro de ciclos no loop (emenda perfeita)."""
    return 0.5 + 0.5 * np.sin(2 * np.pi * cycles * np.arange(N) / N + phase)


def comp_loop(x, thr_db=-20.0, ratio=2.5, sr=MSR):
    """Compressor RMS suave, circular."""
    def f(y):
        env = np.sqrt(lp1(lp1(y ** 2, 4.0, sr), 4.0, sr) + 1e-12)
        lvl = 20 * np.log10(env + 1e-9)
        gdb = np.minimum(0.0, (thr_db - lvl) * (1 - 1 / ratio))
        return y * 10 ** (gdb / 20)
    return circ(x, f)


def master(L, buses, name, ir=None, sends=None, ret=1.0, hicut=7500, rms_db=-16.0):
    """Mixagem final: dry + retorno de reverb, corte de graves/agudos, compressão
    leve, nível RMS coerente entre faixas e pico <= -1 dBFS. Tudo circular."""
    dry = sum(L.get(b) * g for b, g in buses)
    if ir is not None and sends:
        send = sum(L.get(b) * dict(buses).get(b, 1.0) * s for b, s in sends.items())
        dry = dry + rev_loop(send, ir) * ret
    x = circ(dry, lambda y: hp(lp2(y, hicut, MSR), 32, MSR))
    x = x - np.mean(x)
    x = x / (np.sqrt(np.mean(x ** 2)) + 1e-9) * 10 ** (-18 / 20)
    x = comp_loop(x, -20.0, 2.2)
    x = x / (np.sqrt(np.mean(x ** 2)) + 1e-9) * 10 ** (rms_db / 20)
    x = 0.97 * np.tanh(x / 0.97)                    # limitador macio
    pk = np.max(np.abs(x))
    if pk > 10 ** (-1 / 20):
        x *= 10 ** (-1 / 20) / pk
    return write_wav(name, x, MSR)


def add_notes(L, bus, mel, inst, bar0=0, transpose=0, legato=0.95, **kw):
    for b, beat, d, m in mel:
        L.add(bus, inst(m + transpose, d * L.beat * legato, **kw), L.at(bar0 + b, beat))


# ---------------------------------------------------------------------------
# EFEITOS v2 — camadas (transiente + corpo + cauda), passa-baixa, ambiência curta.
# Nível: normalize2() usa o RMS do trecho mais forte (50 ms) -> volumes coerentes.
# ---------------------------------------------------------------------------
def soft_whoosh(dur, f0, f1, f2, peak_at=0.42, q=0.9, body=0.8, blade=0.0, top=6500):
    n = int(dur * SR)
    t = np.linspace(0, 1, n)
    e = np.where(t < peak_at, (t / peak_at) ** 2, np.exp(-(t - peak_at) * 6.5))
    w = whoosh(dur, f0, f1, f2, q=q, peak_at=peak_at)
    air = lp(noise(dur), 380) * e * body * 2.0                 # pressão de ar (corpo grave)
    out = w + air
    if blade > 0:                                              # leve "fio" de lâmina
        out += svf(noise(dur), np.linspace(2600, 1700, n), 5, "bp") * np.sin(np.pi * t) ** 4 * blade
    return lp2(out, top)


def sfx_swing():
    return room(soft_whoosh(0.26, 320, 1600, 480, blade=0.18), 0.25, 0.05), 0.55


def sfx_swing2():
    # golpe de volta: um pouco mais agudo e rápido
    return room(soft_whoosh(0.24, 450, 2000, 650, peak_at=0.38, blade=0.22, top=7000), 0.25, 0.05), 0.55


def sfx_swing3():
    # finalizador: arco largo e pesado com grave crescente
    d = 0.46; n = int(d * SR)
    x = soft_whoosh(d, 170, 1050, 260, peak_at=0.55, q=0.8, body=1.3, blade=0.12, top=5500)
    low = osc(np.linspace(70, 45, n)) * np.sin(np.linspace(0, np.pi, n)) ** 2 * 0.35
    return room(drive(mix(x, low), 1.2), 0.3, 0.06), 0.72


def _impact(d, f0, f1, body_decay, flesh_fc, crunch=0.4, top=6500):
    t = T(d)
    trans = lp(noise(d), 2800) * np.exp(-t / 0.004) * 0.6
    body = thump(d, f0, f1, body_decay)
    flesh = bp(noise(d), flesh_fc, 1.2) * np.exp(-t / 0.035) * 0.9
    cr = bp(crackle(d, 800, decay=0.0015), 1800, 1.0) * np.exp(-t / 0.02) * crunch
    return lp2(drive(mix(trans, body, flesh, cr), 1.3), top)


def sfx_hit():
    return room(_impact(0.3, 150, 55, 0.07, 450), 0.25, 0.07), 1.0


def sfx_hit_crit():
    d = 0.55; t = T(d)
    x = _impact(d, 170, 45, 0.1, 520, crunch=0.55, top=7000) * 1.1
    sub = osc(np.full(len(t), 45.0)) * np.exp(-t / 0.15) * 0.45
    ring = lp(bell(980, d, ratio=1.41, index=1.0, decay=0.15) * 0.16 + bell(1470, d, ratio=1.0, index=0.4, decay=0.1) * 0.08, 6000)
    return room(mix(x, sub, ring), 0.4, 0.1), 1.2


def sfx_hit_heavy():
    d = 0.65; t = T(d)
    x = mix(thump(d, 125, 35, 0.16) * 1.4,
            lp(noise(d), 2200) * np.exp(-t / 0.012) * 0.7,
            bp(noise(d), 380, 1.2) * np.exp(-t / 0.06) * 0.9,
            lp(metal_ring(420, d, 0.12), 4000) * 0.08,
            lp(debris(d, 40, 1100), 4000) * 0.35)
    return room(lp2(drive(x, 1.6), 5500), 0.5, 0.12), 1.25


def sfx_enemy_hit():
    d = 0.24; t = T(d)
    x = mix(thump(d, 170, 75, 0.045) * 0.9,
            bp(noise(d), 650, 1.0) * np.exp(-t / 0.03) * 1.0,
            lp(noise(d), 1200) * np.exp(-t / 0.05) * 0.6 * np.clip(t / 0.004, 0, 1),
            lp(crackle(d, 500, decay=0.0015), 3500) * np.exp(-t / 0.03) * 0.5)
    return room(lp2(drive(x, 1.2), 6000), 0.2, 0.05), 0.85


def sfx_enemy_die():
    d = 0.95; t = T(d)
    out = buf(d)
    poof = lp(noise(d), 900) * np.exp(-t / 0.18) * np.clip(t / 0.01, 0, 1) * 0.9
    put(out, poof, 0)
    put(out, thump(0.45, 110, 45, 0.09) + lp(noise(0.45), 400) * env_exp(0.45, 0.05) * 0.5, 0.3, 0.9)
    r = np.random.default_rng(5)
    for _ in range(6):                                          # ossos/armadura caindo (estalos de madeira)
        put(out, wood(r.uniform(900, 1800), 0.08, 0.6), r.uniform(0.05, 0.5), r.uniform(0.12, 0.25))
    ex = voice(np.linspace(150, 80, int(0.42 * SR)), 0.42, "u", breath=0.5) * adsr(0.42, 0.03, 0.1, 0.6, 0.25)
    put(out, lp(ex, 1800) * 0.25, 0.02)
    return room(lp2(out, 5500), 0.5, 0.14), 0.8


def sfx_step():
    d = 0.12; t = T(d)
    x = mix(lp(noise(d), 500) * np.exp(-t / 0.012) * 1.4,
            thump(d, 85, 55, 0.02) * 0.6,
            bp(crackle(d, 1500, decay=0.001), 2500, 1.0) * np.exp(-t / 0.02) * 0.15)
    return lp2(x, 4000), 0.3


def sfx_footstep_stone():
    d = 0.12; t = T(d)
    x = mix(modal([620, 1450, 2330], [0.5, 0.3, 0.2], [0.012, 0.008, 0.006], d),
            thump(d, 120, 70, 0.018) * 0.6,
            bp(noise(d), 1500, 1.0) * np.exp(-t / 0.008) * 0.5)
    return lp2(x, 6000), 0.3


def sfx_coin():
    d = 0.5
    out = buf(d)
    put(out, coin_clink(2350, d), 0, 0.8)
    put(out, coin_clink(2950, d - 0.07, 0.8), 0.07, 0.6)
    put(out, lp(noise(0.01), 7000) * 0.2, 0)
    return room(lp2(out, 7500), 0.2, 0.08), 0.42


def sfx_ui_click():
    d = 0.08; t = T(d)
    x = wood(1100, d, 0.5) + bp(noise(d), 3000, 1.0) * np.exp(-t / 0.0015) * 0.3
    return lp2(x, 6000), 0.26


def sfx_ui_open():
    d = 0.22; t = T(d)
    out = buf(d)
    sl = 0.11; ts = T(sl)
    slide = bp(noise(sl), 2200, 0.8) * np.sin(np.pi * ts / sl) ** 2 * 0.5   # papel/couro deslizando
    put(out, slide, 0)
    put(out, wood(820, 0.12, 0.8), 0.09, 0.8)
    return lp2(out, 6000), 0.28


def sfx_ui_close():
    d = 0.2
    out = buf(d)
    put(out, wood(700, 0.12, 0.8), 0, 0.8)
    sl = 0.09; ts = T(sl)
    put(out, bp(noise(sl), 1600, 0.8) * np.sin(np.pi * ts / sl) ** 2 * 0.35, 0.03)
    return lp2(out, 5500), 0.26


def sfx_ui_error():
    # duas batidas abafadas e graves (madeira), nada de bipe
    d = 0.3
    out = buf(d)
    put(out, wood(260, 0.15, 1.4) + thump(0.15, 140, 90, 0.03) * 0.4, 0, 1.0)
    put(out, wood(215, 0.15, 1.4) + thump(0.15, 120, 80, 0.03) * 0.4, 0.11, 0.9)
    return lp2(out, 4000), 0.28


def sfx_dash():
    d = 0.28; t = np.linspace(0, 1, int(d * SR))
    x = soft_whoosh(d, 250, 1300, 400, peak_at=0.3, q=0.8, body=1.0)
    cloth = lp(bp(crackle(d, 900, decay=0.001), 3000, 0.8), 5000) * np.sin(np.pi * t) * 0.25
    return lp2(mix(x, cloth), 6000), 0.5


def sfx_block():
    d = 0.4; t = T(d)
    x = mix(modal([310, 720, 1180, 1650], [1, 0.5, 0.3, 0.15], [0.09, 0.05, 0.03, 0.02], d) * 0.8,
            thump(d, 150, 65, 0.05),
            bp(noise(d), 900, 1.0) * np.exp(-t / 0.02) * 0.6)
    return room(lp2(drive(x, 1.2), 6000), 0.3, 0.08), 0.85


def sfx_parry():
    d = 0.8; t = T(d)
    x = mix(bell(1180, d, ratio=1.41, index=1.1, decay=0.25) * 0.5,
            bell(1770, d, ratio=2.76, index=0.5, decay=0.18) * 0.22,
            lp(noise(d), 6000) * np.exp(-t / 0.003) * 0.5,
            fit(svf(noise(0.12), np.linspace(4200, 2500, int(0.12 * SR)), 4, "bp") * np.sin(np.linspace(0, np.pi, int(0.12 * SR))) * 0.15, len(t)))
    return room(lp2(x, 7000), 0.6, 0.16), 0.9


def harp44(m, dur, g=1.0):
    return i_harp(m, dur, g, sr=SR)


def horn44(m, dur, g=1.0, cutoff=1800, att=0.04):
    return i_horn(m, dur, g, att=att, cutoff=cutoff, sr=SR)


def cel44(m, dur, g=1.0, decay=0.6):
    return i_celesta(m, dur, g, sr=SR, decay=decay)


def sfx_levelup():
    d = 2.0
    out = buf(d)
    for i, m in enumerate([60, 64, 67, 72]):
        put(out, horn44(m, 0.16), i * 0.11, 0.8)
        put(out, harp44(m + 12, 1.2), i * 0.11, 0.35)
    st = 0.44
    for m in (60, 64, 67, 72, 76):
        put(out, horn44(m, d - st - 0.3, cutoff=2000, att=0.03), st, 0.32)
    for i, m in enumerate([84, 88, 91, 96]):
        put(out, cel44(m, 1.0, 0.3), st + i * 0.05)
    return room(lp2(out, 7500), 1.2, 0.2), 0.8


def sfx_item_rare():
    d = 1.1
    out = buf(d)
    for i, m in enumerate([79, 83, 86, 91]):
        put(out, harp44(m, 1.0, 0.5), i * 0.06)
        put(out, cel44(m + 12, 0.8, 0.15), i * 0.06 + 0.02)
    return room(lp2(out, 7500), 1.0, 0.22), 0.6


def sfx_item_legendary():
    d = 1.9; t = T(d)
    out = buf(d)
    for i, m in enumerate([72, 76, 79, 84, 88, 91]):
        put(out, harp44(m, 1.4, 0.45), i * 0.06)
    put(out, choir((60, 64, 67, 72), 1.5, "a", att=0.25, rel=0.8) * 0.35, 0.2)
    for m in (84, 88, 91):
        put(out, cel44(m, 1.5, 0.15, 0.9), 0.36)
    put(out, lp(hp(noise(d), 4000), 8000) * adsr(d, 0.4, 0.3, 0.4, 0.8) * 0.04, 0)
    put(out, sine_note(48, 1.3, sr=SR, att=0.2, rel=0.4, harm=0.2) * 0.2, 0.2)
    return room(lp2(out, 7500), 1.6, 0.28), 0.78


def sfx_chest_open():
    d = 1.1; n = int(d * SR); t = np.arange(n) / SR
    f = 85 + 25 * np.sin(2 * np.pi * 1.8 * t) + lp1(rng.uniform(-1, 1, n), 8) * 120
    creak = additive_saw(np.abs(f) + 40, sr=SR, maxh=15) * (0.55 + 0.45 * np.sin(2 * np.pi * 22 * t) ** 2)
    creak = lp(bp(creak, 700, 1.8), 3000) * adsr(d, 0.06, 0.1, 0.7, 0.2) * (t < 0.48)
    out = buf(d)
    put(out, creak * 0.5, 0)
    put(out, coin_clink(1900, 0.2, 0.4) * 0.2, 0.03)
    put(out, wood(160, 0.3, 1.5) * 0.9 + thump(0.3, 120, 55, 0.06) * 0.8, 0.48)
    for i, m in enumerate([84, 88, 91]):
        put(out, cel44(m, 0.6, 0.18), 0.55 + i * 0.05)
    return room(lp2(out, 7000), 0.6, 0.12), 0.65


# ------------------------------------------------------------------ novos
def sfx_goblin_laugh():
    d = 1.0; n = int(d * SR); t = np.arange(n) / SR
    f = np.zeros(n); env = np.zeros(n)
    sylls = [(0.0, 430), (0.15, 410), (0.29, 395), (0.42, 370), (0.56, 350), (0.74, 470)]
    for st, f0 in sylls:
        ln = 0.11 if st < 0.7 else 0.2
        i0, i1 = int(st * SR), int((st + ln) * SR)
        f[i0:i1] = f0 * np.linspace(1.05, 0.9, i1 - i0)
        env[i0:i1] = np.maximum(env[i0:i1], adsr(ln, 0.01, 0.03, 0.7, 0.05)[:i1 - i0])
    f[f == 0] = 400                                     # tom de repouso nos silêncios
    v = voice(f, d, "e", vib=7, vib_amt=0.02, breath=0.3) * 0.6 + voice(f, d, "a", vib=7, vib_amt=0.02, breath=0.2) * 0.4
    asp = bp(noise(d), 1800, 0.8) * 0.25
    hs = np.zeros(n)
    for st, _ in sylls:                                  # "h" aspirado no início de cada sílaba
        i0 = int(st * SR); k = int(0.025 * SR)
        hs[i0:i0 + k] = np.linspace(1, 0, k)
    x = drive(v * env + asp * hs, 1.5)
    return room(lp2(x, 5500), 0.3, 0.1), 0.7


def sfx_goblin_coins():
    d = 1.7; t = T(d)
    out = buf(d)
    r = np.random.default_rng(12)
    for _ in range(45):
        st = r.beta(1.6, 2.6) * 1.3
        put(out, coin_clink(r.uniform(2000, 3600), 0.5, r.uniform(0.6, 1.2)), st, r.uniform(0.12, 0.4))
    pile = bp(crackle(d, 300, decay=0.002), 4000, 0.8) * np.sin(np.pi * np.clip(t / 1.4, 0, 1)) * 0.3
    put(out, pile, 0)
    return room(lp2(out, 7500), 0.5, 0.1), 0.6


def _wood_break(d, r):
    t = T(d)
    out = buf(d)
    put(out, thump(0.4, 130, 55, 0.07), 0, 1.0)
    for _ in range(6):
        put(out, wood(r.uniform(180, 450), 0.25, 1.2), r.uniform(0, 0.12), r.uniform(0.3, 0.6))
    put(out, bp(crackle(d, 600, decay=0.0015), 2800, 0.9) * np.exp(-t / 0.12) * 0.7, 0)
    for _ in range(8):
        put(out, wood(r.uniform(300, 900), 0.1, 0.6), r.uniform(0.2, d - 0.15), r.uniform(0.08, 0.2))
    return out


def sfx_barrel_break():
    d = 0.9
    return room(lp2(_wood_break(d, np.random.default_rng(3)), 7000), 0.5, 0.1), 0.95


def sfx_barrel_explode():
    d = 1.6; t = T(d)
    boom = thump(d, 110, 30, 0.3) * 1.4
    body = svf(noise(d), 250 + 3200 * np.exp(-t / 0.1), 0.8, "lp") * env_exp(d, 0.3) * 1.2
    wb = fit(_wood_break(1.0, np.random.default_rng(8)), len(t)) * 0.6
    deb = lp(debris(d, 60, 1400), 4000) * 0.5
    return room(lp2(drive(mix(boom, body, wb, deb), 1.6), 6500), 0.9, 0.12), 1.3


def sfx_charge_loop():
    # 1,0 s exato e circular: frequências e trêmulo com nº inteiro de ciclos por segundo
    d = 1.0; n = int(d * SR); t = np.arange(n) / SR
    hum = sum(additive_saw(np.full(n, f), sr=SR, maxh=14) * g for f, g in ((110, 1.0), (165, 0.6), (220, 0.35)))
    hum = circ(hum, lambda y: lp(y, 1400))
    trem = 0.75 + 0.25 * np.sin(2 * np.pi * 8 * t)
    shimmer = np.sin(2 * np.pi * 880 * t + 0.8 * np.sin(2 * np.pi * 1760 * t)) * 0.1 * (0.5 + 0.5 * np.sin(2 * np.pi * 3 * t))
    nz = circ(rng.uniform(-1, 1, n), lambda y: bp(y, 1500, 1.5)) * 0.25 * (0.6 + 0.4 * np.sin(2 * np.pi * 4 * t))
    x = hum * trem * 0.5 + shimmer + nz
    return circ(x, lambda y: lp2(y, 6500)), 0.45


def sfx_charge_release():
    d = 1.0; t = T(d)
    x = mix(thump(d, 160, 45, 0.12) * 1.2,
            soft_whoosh(0.4, 600, 3200, 1200, peak_at=0.15, q=1.0, body=0.8, top=7000),
            (bell(880, d, ratio=2.0, index=0.8, decay=0.25) + bell(1320, d, ratio=2.0, index=0.6, decay=0.2)) * 0.15,
            lp(noise(d), 1500) * np.exp(-t / 0.08) * 0.6)
    return room(lp2(drive(x, 1.4), 7000), 0.7, 0.14), 1.0


def sfx_combo_frenzy():
    d = 1.3; t = T(d)
    out = buf(d)
    for i, m in enumerate([60, 64, 67, 72, 76, 79, 84]):
        put(out, horn44(m, 0.12, cutoff=2200, att=0.01), i * 0.07, 0.5)
        put(out, harp44(m + 12, 0.8), i * 0.07, 0.25)
    for s in range(10):
        put(out, lp(tom(SR, f=110 + 4 * s, decay=0.1, dur=0.25, nz=0.4), 3000), s * 0.05, 0.15 + 0.05 * s)
    put(out, lp2(hp(noise(1.0), 3000), 7500) * np.exp(-T(1.0) / 0.35) * 0.25, 0.5)
    put(out, thump(0.5, 140, 45, 0.1) * 0.9, 0.5)
    put(out, soft_whoosh(0.5, 300, 2500, 1500, peak_at=0.9, top=6500) * 0.6, 0.0)
    return room(lp2(out, 7500), 0.8, 0.15), 0.8


def sfx_lockon():
    d = 0.35
    out = buf(d)
    put(out, modal([1600, 4416], [1, 0.3], [0.05, 0.02], 0.2), 0, 0.7)
    put(out, modal([2100, 5796], [1, 0.25], [0.06, 0.02], 0.25), 0.07, 0.6)
    put(out, thump(0.12, 320, 200, 0.02) * 0.4, 0)
    return lp2(out, 6500), 0.4


def sfx_quest_complete():
    d = 2.0
    out = buf(d)
    for t0, m, ln in ((0.0, 67, 0.14), (0.15, 71, 0.14), (0.3, 74, 0.14)):
        put(out, horn44(m, ln), t0, 0.7)
    for m in (55, 62, 67, 71, 79):
        put(out, horn44(m, 1.1, cutoff=1900), 0.45, 0.3)
    for i, m in enumerate([67, 71, 74, 79, 83, 86]):
        put(out, harp44(m, 1.2, 0.3), 0.45 + i * 0.05)
    put(out, cel44(91, 1.2, 0.2, 0.8), 0.45)
    return room(lp2(out, 7500), 1.2, 0.22), 0.8


def sfx_achievement():
    d = 2.2
    out = buf(d)
    for i, m in enumerate([84, 88, 91, 96]):
        put(out, cel44(m, 1.6, 0.35, 0.9), i * 0.07)
    for m in (72, 76, 79):
        put(out, bell(mtof(m), 1.8, ratio=2.0, index=0.6, decay=0.7) * 0.15, 0.3)
    put(out, choir((72, 76, 79), 1.6, "a", att=0.3, rel=0.6) * 0.3, 0.25)
    put(out, lp(hp(noise(1.6), 5000), 8000) * adsr(1.6, 0.4, 0.3, 0.4, 0.6) * 0.035, 0.2)
    return room(lp2(out, 7500), 1.8, 0.3), 0.75


def sfx_grade_s():
    d = 1.6
    out = buf(d)
    for t0, ln in ((0.0, 0.1), (0.13, 0.1)):
        for m in (67, 71, 74):
            put(out, horn44(m, ln, cutoff=2200, att=0.01), t0, 0.4)
    for m in (60, 67, 72, 76, 79):
        put(out, horn44(m, 1.0, cutoff=2300, att=0.02), 0.28, 0.3)
    put(out, lp(tom(SR, f=70, decay=0.3, dur=0.8, nz=0.2), 2500) * 0.8, 0.28)
    put(out, lp(tom(SR, f=70, decay=0.15, dur=0.4, nz=0.2), 2500) * 0.4, 0.0)
    put(out, lp2(hp(noise(1.2), 3000), 7500) * np.exp(-T(1.2) / 0.4) * 0.2, 0.28)
    return room(lp2(out, 7500), 1.0, 0.2), 0.88


def sfx_page_turn():
    d = 0.55; t = T(d)
    rust = bp(crackle(d, 2500, decay=0.0008) + noise(d) * 0.3, 3200, 0.7)
    e1 = np.exp(-((t - 0.12) / 0.07) ** 2)
    flop = bp(noise(d), 1500, 0.8) * np.exp(-((t - 0.34) / 0.06) ** 2) * 0.8
    out = rust * e1 * 0.6 + flop
    put(out, wood(500, 0.1, 0.8) * 0.25, 0.42)
    return lp2(out, 7000), 0.35


def sfx_boss_phase():
    d = 2.8; t = T(d)
    out = buf(d)
    sw = 0.6
    put(out, lp2(hp(noise(sw), 1500), 6000) * (T(sw) / sw) ** 3 * 0.4, 0)
    put(out, lp(gong(70, d - sw, decay=1.4, bright=0.6), 4000) * 0.6, sw)
    put(out, thump(1.2, 80, 28, 0.5) * 1.2, sw)
    put(out, choir((40, 47, 52), 1.9, "o", att=0.15, rel=0.8) * 0.6, sw)
    return room(lp2(drive(out, 1.3), 6500), 2.0, 0.22), 1.0


V2_IDS = ["swing", "swing2", "swing3", "hit", "hit_crit", "hit_heavy", "enemy_hit", "enemy_die", "step",
          "footstep_stone", "coin", "ui_click", "ui_open", "ui_close", "ui_error", "dash", "block", "parry",
          "levelup", "item_rare", "item_legendary", "chest_open",
          "goblin_laugh", "goblin_coins", "barrel_break", "barrel_explode", "charge_loop", "charge_release",
          "combo_frenzy", "lockon", "quest_complete", "achievement", "grade_s", "page_turn", "boss_phase"]
LOOP_SFX = {"charge_loop"}


def trim_tail(x, db=-54.0):
    """Corta a cauda abaixo de 'db' (relativo ao pico) — arquivos menores."""
    a = np.abs(x)
    idx = np.nonzero(a > np.max(a) * 10 ** (db / 20))[0]
    end = min(len(x), (idx[-1] if len(idx) else len(x)) + int(0.03 * SR))
    return x[:end]


def normalize2(x, level):
    """RMS do trecho mais forte (50 ms) -> alvo proporcional a 'level'; pico <= -1 dBFS."""
    x = x - np.mean(x)
    w = int(0.05 * SR)
    p = np.convolve(x ** 2, np.ones(w) / w, mode="same")
    rms = np.sqrt(np.max(p)) + 1e-9
    g = (0.32 * level) / rms
    pk = np.max(np.abs(x)) * g
    if pk > 10 ** (-1 / 20):
        g *= 10 ** (-1 / 20) / pk
    return x * g


# ---------------------------------------------------------------------------
# MÚSICAS POR ÁREA (22050 Hz mono, loops perfeitos com comprimento exato em compassos)
# ---------------------------------------------------------------------------
def music_menu():
    # Tema principal — Ré menor, 90 bpm, 16 compassos (42,7 s). Trompa com o tema,
    # cordas + harpa, coro na parte B, tímpanos e pratos levando de volta ao início.
    L = Loop(90, 16)
    names = ["Dm", "Bb", "F", "C", "Dm", "Bb", "Gm", "A", "Bb", "C", "Dm", "A", "Gm", "Bb", "Asus4", "A"]
    prog = [CH(n) for n in names]
    mel = [(0, 0, 1.5, 74), (0, 1.5, .5, 69), (0, 2, 1, 74), (0, 3, 1, 76),
           (1, 0, 3, 77), (1, 3, 1, 76),
           (2, 0, 2, 72), (2, 2, 1, 77), (2, 3, 1, 79),
           (3, 0, 4, 76),
           (4, 0, 1.5, 74), (4, 1.5, .5, 69), (4, 2, 1, 74), (4, 3, 1, 76),
           (5, 0, 2, 77), (5, 2, 1, 79), (5, 3, 1, 81),
           (6, 0, 2, 82), (6, 2, 1, 81), (6, 3, 1, 79),
           (7, 0, 2, 76), (7, 2, 2, 73),
           (8, 0, 1.5, 74), (8, 1.5, .5, 77), (8, 2, 2, 82),
           (9, 0, 1.5, 79), (9, 1.5, .5, 76), (9, 2, 2, 72),
           (10, 0, 1.5, 77), (10, 1.5, .5, 76), (10, 2, 1, 74), (10, 3, 1, 72),
           (11, 0, 2, 73), (11, 2, 2, 76),
           (12, 0, 1, 79), (12, 1, 1, 77), (12, 2, 1, 79), (12, 3, 1, 82),
           (13, 0, 2, 77), (13, 2, 1, 74), (13, 3, 1, 70),
           (14, 0, 2, 74), (14, 2, 2, 73),
           (15, 0, 2, 76), (15, 2, 1, 73), (15, 3, 1, 69)]
    bar = 4 * L.beat
    for b, ch in enumerate(prog):
        B = b >= 8
        L.add("pad", i_pad(vchord(ch, 53), bar, 0.55, att=0.5, rel=1.0, cutoff=2100 if B else 1500), L.at(b))
        bs = bass_of(ch)
        L.add("low", i_bow(bs, bar * 0.98, 0.5, att=0.15, rel=0.4, cutoff=900), L.at(b))
        L.add("low", sine_note(bs - 12, bar * 0.9, att=0.05, rel=0.3, harm=0.2) * 0.35, L.at(b))
        arp = vchord(ch, 57) + [vchord(ch, 57)[0] + 12]
        seq = [0, 1, 2, 3, 2, 1, 2, 3] if b % 2 == 0 else [3, 2, 1, 0, 1, 2, 3, 2]
        for i, k in enumerate(seq):
            L.add("harp", i_harp(arp[k % len(arp)] + (12 if B and i % 4 == 3 else 0), 1.6, 0.32), L.at(b, i * 0.5))
        if B:
            L.add("choir", i_choir(vchord(ch, 50), bar, 0.5, "o", att=0.5, rel=0.8), L.at(b))
        # tímpanos
        L.add("perc", p_taiko(mtof(bs if bs >= 36 else bs + 12), 0.9, 0.5), L.at(b))   # tímpano afinado na fundamental
        if B:
            L.add("perc", p_taiko(70, 0.4, 0.3), L.at(b, 2.5))
        if b in (7, 15):
            for s in range(8):
                L.add("perc", p_taiko(80, 0.18 + 0.06 * s, 0.18), L.at(b, 2 + s * 0.25))
            L.add("cym", p_swell(bar * 0.98, 0.35), L.at(b))
        if b in (0, 8):
            L.add("cym", p_cym(0.3, dur=2.5), L.at(b))
        if b % 4 == 0:
            L.add("cel", i_celesta(86 if b % 8 == 0 else 81, 2.5, 0.25), L.at(b, 1.5))
    add_notes(L, "horn", [n for n in mel if n[0] < 8], i_horn, transpose=-12, g=0.75, cutoff=1500, att=0.09)
    add_notes(L, "horn", [n for n in mel if n[0] >= 8], i_horn, transpose=-12, g=0.7, cutoff=1700, att=0.08)
    add_notes(L, "lead", [n for n in mel if n[0] >= 8], i_bow, g=0.32, att=0.1, vib=0.007, cutoff=3000)
    ir = make_ir(2.4)
    return master(L, [("pad", 0.8), ("low", 0.8), ("harp", 0.9), ("choir", 0.7), ("perc", 0.9), ("cym", 0.6),
                      ("cel", 0.7), ("horn", 1.0), ("lead", 0.7)], "music_menu", ir,
                  {"pad": 0.5, "harp": 0.45, "choir": 0.6, "perc": 0.35, "horn": 0.35, "lead": 0.4, "cel": 0.6, "low": 0.2})


def music_town():
    # Praça — Ré mixolídio, 112 bpm, 20 compassos (42,9 s). Alaúde + flauta, tambor de moldura.
    L = Loop(112, 20)
    A = [CH(n) for n in ("D", "C", "G", "D")]
    Bp = [CH(n) for n in ("Em", "G", "C", "D")]
    sc = scale_notes(62, MIXO, 62, 81)
    pats = [[1, .5, .5, 1, 1], [.5, .5, .5, .5, 1, 1], [1.5, .5, 1, 1], [.5, .5, 1, .5, .5, 1], [1, 1, 1, 1]]
    melA = gen_melody(A, sc, 11, pats, cadence=(1, 1, 2))
    melA2 = gen_melody(A, sc, 31, pats, cadence=(1, 1, 2))
    melB = gen_melody(Bp, sc, 23, pats, cadence=(1, 1, 2))
    form = [(A, melA), (A, melA2), (Bp, melB), (A, melA), (Bp, melB)]
    bar0 = 0
    for si, (prog, mel) in enumerate(form):
        isB = prog is Bp
        add_notes(L, "lute", mel, lambda m, d, **k: i_lute(m, d + 0.35, **k), bar0, g=0.6, bright=0.55)
        if isB or si == 3:
            add_notes(L, "flute", mel, i_flute, bar0, transpose=12, g=0.22 if isB else 0.14, legato=0.92)
        for b, ch in enumerate(prog):
            bar = bar0 + b
            for beat in (0, 2, 3.5):
                for k, m in enumerate(vchord(ch, 55) + [vchord(ch, 55)[0] + 12]):
                    L.add("strum", i_lute(m, 0.9, 0.2, bright=0.35, t60=0.7), L.at(bar, beat) + int(k * 0.014 * MSR))
            bs = bass_of(ch)
            for beat, m in ((0, bs), (2, bs + 7), (3, bs)):
                L.add("bass", i_pizz(m, 0.8, 0.7), L.at(bar, beat))
            for beat, v in ((0, 1.0), (1.5, 0.45), (2, 0.8), (3.5, 0.4)):
                L.add("drum", p_frame(0.5 * v, f=105), L.at(bar, beat))
            for e in range(8):
                L.add("shk", p_shaker(0.10 if e % 2 else 0.05), L.at(bar, e * 0.5))
            for beat in (1, 3):
                L.add("shk", lp(tambourine(MSR), 6500, MSR) * 0.12, L.at(bar, beat))
        bar0 += len(prog)
    ir = make_ir(1.3)
    return master(L, [("lute", 1.0), ("flute", 0.9), ("strum", 0.7), ("bass", 0.8), ("drum", 0.8), ("shk", 0.7)],
                  "music_town", ir, {"lute": 0.3, "flute": 0.4, "strum": 0.25, "drum": 0.2})


def music_guild():
    # Taverna — Sol maior em 3/4, 138 bpm, 32 compassos (41,7 s). Rabeca, alaúde "um-pa-pa", flauta na B.
    L = Loop(138, 32, beats=3)
    A = [CH(n) for n in ("G", "D", "Em", "C", "G", "C", "D", "D")]
    Bp = [CH(n) for n in ("C", "G", "Am", "Em", "C", "G", "Am", "D")]
    sc = scale_notes(67, MAJOR, 67, 86)
    pats = [[1, 1, 1], [2, 1], [1.5, .5, 1], [1, .5, .5, 1], [.5, .5, 1, 1]]
    melA = gen_melody(A, sc, 5, pats, strong=(0.0,), cadence=(3,))
    melA2 = gen_melody(A, sc, 15, pats, strong=(0.0,), cadence=(2, 1))
    melB = gen_melody(Bp, sc, 8, pats, strong=(0.0,), cadence=(2, 1))
    form = [(A, melA, "fid"), (A, melA2, "fid"), (Bp, melB, "fl"), (A, melA, "fid")]
    bar0 = 0
    for prog, mel, who in form:
        if who == "fid":
            add_notes(L, "fid", mel, i_bow, bar0, g=0.42, att=0.03, rel=0.1, vib=0.008, cutoff=3000, legato=0.9)
        else:
            add_notes(L, "fl", mel, i_flute, bar0, g=0.3, legato=0.92)
            add_notes(L, "fid", mel, i_bow, bar0, transpose=-12, g=0.18, att=0.05, rel=0.1, cutoff=2000, legato=0.9)
        for b, ch in enumerate(prog):
            bar = bar0 + b
            bs = bass_of(ch)
            L.add("lute", i_lute(bs + 12, 1.2, 0.55, bright=0.45), L.at(bar, 0))
            L.add("bass", sine_note(bs, L.beat * 0.9, att=0.01, rel=0.2, harm=0.3) * 0.45, L.at(bar, 0))
            for beat in (1, 2):
                for k, m in enumerate(vchord(ch, 57)):
                    L.add("lute", i_lute(m, 0.5, 0.16, bright=0.4, t60=0.5), L.at(bar, beat) + int(k * 0.01 * MSR))
            L.add("drum", p_taiko(95, 0.45, 0.2, cutoff=1800), L.at(bar, 0))
            L.add("drum", p_tek(0.12), L.at(bar, 1)); L.add("drum", p_tek(0.09), L.at(bar, 2))
            for e in range(6):
                L.add("shk", p_shaker(0.07 if e % 2 else 0.035), L.at(bar, e * 0.5))
        bar0 += len(prog)
    ir = make_ir(1.0, damp=4000)
    return master(L, [("fid", 1.0), ("fl", 0.9), ("lute", 0.9), ("bass", 0.8), ("drum", 0.8), ("shk", 0.6)],
                  "music_guild", ir, {"fid": 0.35, "fl": 0.4, "lute": 0.3, "drum": 0.25}, rms_db=-17)


def music_forest():
    # Bosque Esmeralda — Mi menor (dórico), 68 bpm, 12 compassos (42,4 s). Harpa, cordas suaves, flauta, vento.
    L = Loop(68, 12)
    names = ["Em", "Cmaj7", "Am7", "Bsus4", "Em", "Cmaj7", "D", "Bm", "Am", "Em", "Cmaj7", "B"]
    prog = [CH(n) for n in names]
    r = np.random.default_rng(42)
    bar = 4 * L.beat
    for b, ch in enumerate(prog):
        L.add("pad", i_pad(vchord(ch, 52), bar, 0.5, att=1.4, rel=1.6, cutoff=1100, det=0.007), L.at(b))
        bs = bass_of(ch)
        L.add("low", i_bow(bs, bar * 0.97, 0.35, att=0.6, rel=0.8, cutoff=600, vib=0.003), L.at(b))
        notes = vchord(ch, 52) + [x + 12 for x in vchord(ch, 52)]
        seq = [0, 2, 4, 5, 3, 4, 2, 1] if b % 2 == 0 else [1, 3, 5, 4, 2, 3, 1, 2]
        for i, k in enumerate(seq):
            if r.random() < 0.88:
                L.add("harp", i_harp(notes[k % len(notes)], 2.4, 0.3 * (1.0 if i % 4 == 0 else 0.75)), L.at(b, i * 0.5))
        for _ in range(r.integers(1, 3)):
            m = int(r.choice(vchord(ch, 81)))
            L.add("cel", i_celesta(m, 2.0, 0.12, decay=1.1), L.at(b, float(r.choice([0.5, 1.5, 2.5, 3.0, 3.5]))))
    sc = scale_notes(64, DORIAN, 64, 83)
    mel = gen_melody(prog[4:12], sc, 5, [[2, 2], [3, 1], [1, 1, 2], [4], [2, 1, 1]], cadence=(4,))
    add_notes(L, "fl", mel, i_flute, 4, g=0.3, legato=0.95, vib=0.007, breath=0.13)
    N = L.N
    nz = loop_noise(N)
    wind = circ(nz, lambda y: lp(y, 600, MSR)) * (0.3 + 0.7 * lfo_loop(N, 3)) + \
        circ(nz, lambda y: bp(y, 1400, 1.2, MSR)) * 0.25 * lfo_loop(N, 2, 1.3)
    L.bus["wind"] = wind * 0.35
    ir = make_ir(2.8, damp=4500)
    return master(L, [("pad", 0.8), ("low", 0.6), ("harp", 1.0), ("cel", 0.7), ("fl", 0.9), ("wind", 0.6)],
                  "music_forest", ir, {"pad": 0.5, "harp": 0.5, "cel": 0.7, "fl": 0.5, "low": 0.3}, rms_db=-18)


def music_crypt():
    # Ruínas / cripta — Ré menor frígio, 56 bpm, 10 compassos (42,9 s). Coro grave, sinos distantes, drone.
    L = Loop(56, 10)
    names = ["Dm", "Eb", "Dm", "Bbm", "Dm", "Gm", "Eb", "A", "Dm", "Eb"]
    prog = [CH(n) for n in names]
    N = L.N
    t = np.arange(N) / MSR
    drone = np.zeros(N)
    for m, g in ((38, 1.0), (45, 0.55), (26, 0.5)):
        for det, gg in ((1.0, 1.0), (1.004, 0.7)):
            f = round(mtof(m) * det * N / MSR) * MSR / N    # nº inteiro de ciclos no loop
            ph = 2 * np.pi * f * t
            drone += sum(np.sin(k * ph) / k ** 1.4 for k in range(1, 10)) * g * gg
    lfo = lfo_loop(N, 2)
    dark = circ(drone, lambda y: lp(y, 180, MSR)); brt = circ(drone, lambda y: lp(y, 520, MSR))
    L.bus["drone"] = (dark + (brt - dark) * lfo) * 0.22
    bar = 4 * L.beat
    r = np.random.default_rng(9)
    hm = scale_notes(62, PHRYG, 50, 69)
    for b, ch in enumerate(prog):
        L.add("choir", i_choir(vchord(ch, 50), bar, 0.55, "u" if b % 2 == 0 else "o", att=1.2, rel=1.5), L.at(b))
        L.add("choir", i_choir([bass_of(ch)], bar, 0.4, "o", att=1.0, rel=1.2, per=2), L.at(b))
        if b % 2 == 0:
            L.add("bell", i_bell(mtof(62 if b % 4 == 0 else 57), 6.0, 0.35, decay=2.5, cutoff=1800), L.at(b, 0.5))
        for k in range(2):
            if r.random() < 0.75:
                m = int(r.choice(hm))
                L.add("pluck", lp(i_harp(m - 12, 3.0, 0.4), 1500, MSR), L.at(b, k * 2 + float(r.choice([0, 0.5, 1]))))
        L.add("heart", p_taiko(48, 0.55, 0.35, cutoff=300), L.at(b, 0))
        L.add("heart", p_taiko(46, 0.35, 0.3, cutoff=300), L.at(b, 0.4))
    ir = make_ir(3.6, damp=3500)
    return master(L, [("drone", 0.9), ("choir", 1.0), ("bell", 0.8), ("pluck", 0.8), ("heart", 0.9)],
                  "music_crypt", ir, {"choir": 0.6, "bell": 0.9, "pluck": 0.6, "heart": 0.4}, rms_db=-18)


def music_desert():
    # Deserto — Mi hijaz, 100 bpm, 16 compassos (38,4 s). Alaúde árabe (oud), ney, darbuka (maqsum).
    L = Loop(100, 16)
    names = ["E", "E", "F", "E", "Dm", "Dm", "F", "E"]
    prog = [CH(n) for n in names]
    sc = scale_notes(64, HIJAZ, 64, 83)
    melA = gen_melody(prog, sc, 4, [[.5, .5, 1, .5, .5, 1], [1, .5, .5, 2], [.25, .25, .5, 1, 1, 1], [1.5, .5, 1, 1]],
                      cadence=(2, 2), leaps=(-1, -1, 1, 1, -2, 2))
    melB = gen_melody(prog, sc, 9, [[2, 2], [1, 1, 2], [3, 1], [1.5, .5, 2]], cadence=(4,), leaps=(-1, 1, -1, 1))
    add_notes(L, "oud", melA, lambda m, d, **k: i_oud(m, d + 0.25, **k), 0, g=0.6)
    add_notes(L, "ney", melB, i_flute, 8, g=0.3, breath=0.22, vib=0.01, h2=0.12, att=0.09)
    add_notes(L, "oud", melA, lambda m, d, **k: i_oud(m, d + 0.25, **k), 8, transpose=-12, g=0.3)
    bar = 4 * L.beat
    for b in range(16):
        ch = prog[b % 8]
        if b % 4 == 0:
            L.add("drone", i_pad([40, 47], bar * 4, 0.5, att=1.0, rel=1.5, cutoff=700, det=0.004), L.at(b))
        bs = bass_of(ch)
        L.add("bass", i_oud(bs, 0.9, 0.45), L.at(b, 0)); L.add("bass", i_oud(bs, 0.6, 0.3), L.at(b, 1.5))
        L.add("bass", i_oud(bs + 7, 0.6, 0.25), L.at(b, 2.5))
        # maqsum: DUM tek _ tek DUM _ tek _
        for beat, kind, v in ((0, "d", 1.0), (0.5, "t", 0.7), (1.5, "t", 0.6), (2, "d", 0.85), (3, "t", 0.7), (3.5, "t", 0.35)):
            L.add("darb", p_taiko(100, 0.6 * v, 0.18, cutoff=2000) if kind == "d" else p_tek(0.3 * v), L.at(b, beat))
        L.add("darb", p_frame(0.35, f=80), L.at(b, 0))
        for beat in (1, 3):
            L.add("riq", lp(tambourine(MSR), 6000, MSR) * 0.1, L.at(b, beat))
        if b % 2 == 0:
            L.add("riq", modal([2600, 3900, 5200], [1, 0.5, 0.2], [0.5, 0.3, 0.2], 1.2, MSR) * 0.07, L.at(b, 0))
    ir = make_ir(1.5, damp=4500)
    return master(L, [("oud", 1.0), ("ney", 0.9), ("drone", 0.6), ("bass", 0.8), ("darb", 0.85), ("riq", 0.7)],
                  "music_desert", ir, {"oud": 0.3, "ney": 0.45, "drone": 0.3, "darb": 0.2, "riq": 0.3})


def music_snow():
    # Neve — Lá menor glacial, 72 bpm, 12 compassos (40 s). Celesta, pads frios, sinos de vidro, vento.
    L = Loop(72, 12)
    names = ["Amadd9", "Fmaj7", "Cadd9", "G6", "Am", "Em", "Fmaj7", "Gsus4", "Fmaj7", "Cadd9", "Dm7", "Esus4"]
    prog = [CH(n) for n in names]
    r = np.random.default_rng(77)
    bar = 4 * L.beat
    for b, ch in enumerate(prog):
        L.add("pad", i_pad(vchord(ch, 57), bar, 0.55, att=1.8, rel=2.0, cutoff=950, det=0.011, voices=4), L.at(b))
        L.add("sub", sine_note(bass_of(ch), bar * 0.95, att=0.8, rel=1.0, harm=0.1) * 0.35, L.at(b))
        notes = vchord(ch, 69) + [x + 12 for x in vchord(ch, 69)]
        for i in range(8):
            if r.random() < 0.7:
                k = (i * 2 + b) % len(notes) if i % 2 == 0 else int(r.integers(len(notes)))
                L.add("cel", i_celesta(notes[k], 2.2, 0.22 if i % 2 == 0 else 0.15, decay=1.2), L.at(b, i * 0.5))
        if b % 3 == 0:
            L.add("glass", bell(mtof(int(r.choice(vchord(ch, 84)))), 3.0, ratio=3.5, index=0.7, decay=1.0, sr=MSR) * 0.08, L.at(b, 2.5))
    sc = scale_notes(69, MINOR, 69, 88)
    mel = gen_melody(prog[4:12], sc, 21, [[2, 2], [3, 1], [4], [1, 1, 2]], cadence=(4,), leaps=(-1, 1, -2, 2))
    add_notes(L, "lead", mel, i_flute, 4, g=0.22, att=0.25, breath=0.03, vib=0.004, h2=0.05, rel=0.6)
    N = L.N
    nz = loop_noise(N)
    L.bus["wind"] = (circ(nz, lambda y: bp(y, 1100, 0.9, MSR)) * (0.2 + 0.8 * lfo_loop(N, 3, 0.4)) +
                     circ(nz, lambda y: lp(y, 350, MSR)) * 0.8) * 0.18
    ir = make_ir(4.0, damp=5500)
    return master(L, [("pad", 0.8), ("sub", 0.7), ("cel", 1.0), ("glass", 0.8), ("lead", 0.9), ("wind", 0.6)],
                  "music_snow", ir, {"pad": 0.5, "cel": 0.6, "glass": 0.8, "lead": 0.55}, rms_db=-18)


def music_ruins_city():
    # Cidade destruída — Dó menor, 76 bpm, 12 compassos (37,9 s). Violoncelo melancólico,
    # cordas, tambores de guerra distantes e trompa ao longe.
    L = Loop(76, 12)
    names = ["Cm", "Ab", "Eb", "G", "Fm", "Cm", "Ab", "G", "Cm", "Ab", "Fm", "G"]
    prog = [CH(n) for n in names]
    sc = scale_notes(60, HARM, 48, 70)
    mel = gen_melody(prog, sc, 12, [[2, 2], [3, 1], [4], [1, 1, 2], [2, 1, 1]], cadence=(4,), leaps=(-1, 1, -1, 1, 2, -2))
    add_notes(L, "cello", mel, i_bow, 0, g=0.5, att=0.18, rel=0.4, vib=0.008, cutoff=1800, legato=0.98)
    bar = 4 * L.beat
    for b, ch in enumerate(prog):
        L.add("pad", i_pad(vchord(ch, 55), bar, 0.45, att=1.0, rel=1.4, cutoff=1100), L.at(b))
        L.add("low", sine_note(bass_of(ch), bar * 0.95, att=0.3, rel=0.8, harm=0.2) * 0.4, L.at(b))
        for beat, v in ((0, 1.0), (1.5, 0.55), (2, 0.8)):
            L.add("war", p_taiko(55, 0.8 * v, 0.5, cutoff=380), L.at(b, beat))
        if b % 4 == 3:
            for s in range(4):
                L.add("war", p_taiko(60, 0.3 + 0.1 * s, 0.35, cutoff=380), L.at(b, 3 + s * 0.25))
        for beat in (0, 1.5, 3):
            m = vchord(ch, 60)[int(beat) % len(vchord(ch, 60))]
            L.add("harp", i_harp(m, 2.0, 0.18), L.at(b, beat))
        if b in (3, 7, 11):
            for i, (m, d) in enumerate(((55, 0.8), (60, 0.8), (63, 1.8))):
                L.add("horn", lp(i_horn(m, d * L.beat * 1.6, 0.35, att=0.12, cutoff=900), 900, MSR), L.at(b - 1, 1 + i * 1.0))
    ir = make_ir(2.6, damp=4000)
    return master(L, [("cello", 1.0), ("pad", 0.7), ("low", 0.7), ("war", 0.5), ("harp", 0.7), ("horn", 0.5)],
                  "music_ruins_city", ir, {"cello": 0.4, "pad": 0.5, "war": 1.2, "harp": 0.5, "horn": 1.2}, rms_db=-17)


def music_lava():
    # Lava — Mi frígio, 132 bpm, 24 compassos (43,6 s). Baixo distorcido pesado, taikos, metais graves, coro.
    L = Loop(132, 24)
    names = ["Em", "F", "Em", "Dm", "Em", "F", "G", "F"] * 3
    prog = [CH(n) for n in names]
    sc = scale_notes(64, PHRYG, 59, 79)
    mel = gen_melody(prog[8:16], sc, 3, [[1, 1, 2], [2, 1, 1], [1.5, .5, 2], [3, 1]], cadence=(4,))
    mel2 = gen_melody(prog[16:24], sc, 13, [[1, 1, 2], [2, 2], [1.5, .5, 1, 1]], cadence=(4,))
    add_notes(L, "lead", mel, i_horn, 8, g=0.7, cutoff=1600, att=0.05)
    add_notes(L, "lead", mel2, i_horn, 16, g=0.7, cutoff=1700, att=0.05)
    add_notes(L, "lead", mel2, i_bow, 16, transpose=12, g=0.18, cutoff=2500, att=0.05)
    bar = 4 * L.beat
    for b, ch in enumerate(prog):
        rt = bass_of(ch) - 12 if bass_of(ch) - 12 >= 26 else bass_of(ch)
        for e, off in enumerate((0, 0, 12, 0, 1, 0, 12, 0)):
            n = int(0.2 * MSR)
            s = additive_saw(np.full(n, mtof(rt + off + 12 * 0)), sr=MSR, maxh=20)
            s = drive(lp(s, 650, MSR) * 1.5, 2.2) * env_exp(0.2, 0.09, MSR, 0.003)
            s = fades(s, 0.002, 0.03, MSR)                 # fim da nota sem clique
            L.add("bass", s * (0.55 if e % 4 == 0 else 0.4), L.at(b, e * 0.5))
        L.add("sub", sine_note(rt, bar * 0.95, att=0.02, rel=0.1, harm=0.0) * 0.4, L.at(b))
        for beat, v in ((0, 1.0), (0.75, 0.5), (1.5, 0.6), (2, 0.9), (3, 0.6), (3.5, 0.5)):
            L.add("taiko", p_taiko(58, 0.9 * v, 0.35, cutoff=1500), L.at(b, beat))
        L.add("kick", p_kick(0.8), L.at(b, 0)); L.add("kick", p_kick(0.6), L.at(b, 2))
        if b >= 8:
            for beat in (1, 3):
                L.add("kick", p_snare(0.35, decay=0.1), L.at(b, beat))
        if b >= 4:
            for beat, d in ((0, 0.9), (2.5, 0.4)):
                L.add("brass", sum(i_horn(m, d * L.beat, 0.35, att=0.02, cutoff=900) for m in vchord(ch, 40)), L.at(b, beat))
        if b >= 12:
            L.add("choir", i_choir(vchord(ch, 52), bar, 0.5, "a", att=0.3, rel=0.6), L.at(b))
        if b % 2 == 1:
            L.add("anvil", lp(modal([1100, 2640, 4290], [1, 0.5, 0.25], [0.25, 0.15, 0.08], 0.8, MSR), 4000, MSR) * 0.18, L.at(b, 3.5))
        if b in (7, 15, 23):
            L.add("cym", p_swell(bar * 0.98, 0.3), L.at(b))
        if b in (0, 8, 16):
            L.add("cym", p_cym(0.25), L.at(b))
    ir = make_ir(1.7, damp=4000)
    return master(L, [("bass", 0.9), ("sub", 0.8), ("taiko", 0.9), ("kick", 0.8), ("brass", 0.8), ("choir", 0.7),
                      ("lead", 1.0), ("anvil", 0.7), ("cym", 0.5)], "music_lava", ir,
                  {"taiko": 0.25, "brass": 0.3, "choir": 0.5, "lead": 0.35, "anvil": 0.5}, rms_db=-15)


def _boss_like(name, bpm, form, progs, leads, lead_phr, choir_from, intensity):
    """Base comum dos chefes: ostinato de cordas em semicolcheias, baixo, metais,
    tema na trompa, coro, taikos/bateria e pratos marcando as frases."""
    bars = len(form) * 4
    L = Loop(bpm, bars)
    bar = 4 * L.beat
    for ph, key in enumerate(form):
        prog = progs[key]
        for b, ch in enumerate(prog):
            br = ph * 4 + b
            vc = vchord(ch, 52)
            for s in range(16):
                m = vc[[0, 1, 2, 1][s % 4] % len(vc)] + (12 if (intensity > 1 and s % 8 >= 4) else 0)
                acc = 1.0 if s % 4 == 0 else 0.65
                L.add("spic", i_bow(m, 0.07, 0.22 * acc, att=0.004, rel=0.06, vib=0.0, cutoff=2600), L.at(br, s * 0.25))
            bs = bass_of(ch)
            for e in range(8):
                m = bs - 12 + (12 if e in (3, 7) else 0)
                if m < 28:
                    m += 12
                n = int(0.22 * MSR)
                s_ = drive(lp(additive_saw(np.full(n, mtof(m)), sr=MSR, maxh=20), 800, MSR), 1.6) * env_exp(0.22, 0.09, MSR, 0.003)
                s_ = fades(s_, 0.002, 0.03, MSR)
                L.add("bass", s_ * 0.45, L.at(br, e * 0.5))
            if ph > 0:
                for beat, d in ((0, 0.9), (1.5, 0.45)):
                    L.add("brass", sum(i_horn(m, d * L.beat, 0.3, att=0.015, cutoff=1500) for m in vchord(ch, 48)), L.at(br, beat))
            if ph >= choir_from:
                L.add("choir", i_choir(vchord(ch, 52), bar, 0.45, "a", att=0.15, rel=0.4), L.at(br))
                if intensity > 1:
                    L.add("choir", i_choir([bs], bar, 0.35, "o", att=0.15, rel=0.4, per=2), L.at(br))
            if intensity > 1:
                L.add("pad", i_pad(vchord(ch, 60), bar, 0.3, att=0.1, rel=0.3, cutoff=2200, bright=0.8), L.at(br))
            # bateria
            L.add("kick", p_kick(1.0, f0=140), L.at(br, 0)); L.add("kick", p_kick(0.8, f0=140), L.at(br, 2))
            L.add("kick", p_kick(0.55, f0=140), L.at(br, 2.5 if b % 2 else 1.5))
            for beat in (1, 3):
                L.add("snare", p_snare(0.55), L.at(br, beat))
            if intensity > 1:
                for beat in (1.75, 3.75):
                    L.add("snare", p_snare(0.15), L.at(br, beat))
            for e in range(8):
                L.add("hat", p_hat(0.16 if e % 2 else 0.09), L.at(br, e * 0.5))
            L.add("taiko", p_taiko(60, 0.8, 0.4), L.at(br, 0))
            if b == 3:
                for s in range(16 if intensity > 1 else 8):
                    st = s * 0.25 if intensity > 1 else 2 + s * 0.25
                    L.add("taiko", p_taiko(95 - s * 2, 0.25 + 0.03 * s, 0.15), L.at(br, st))
                L.add("cym", p_swell(bar * 0.98, 0.3), L.at(br))
            if b == 0 and ph > 0:
                L.add("cym", p_cym(0.3), L.at(br))
        if ph in lead_phr:
            mel = leads[lead_phr[ph]]
            add_notes(L, "lead", mel, i_horn, ph * 4, transpose=-12, g=0.75, cutoff=2000, att=0.03)
            if intensity > 1:
                add_notes(L, "lead", mel, i_horn, ph * 4, transpose=0, g=0.35, cutoff=2400, att=0.03)
                add_notes(L, "lead", mel, i_bow, ph * 4, transpose=0, g=0.15, cutoff=3000, att=0.04)
    ir = make_ir(1.6, damp=4500)
    return master(L, [("spic", 0.8), ("bass", 0.9), ("brass", 0.75), ("choir", 0.75), ("pad", 0.5), ("kick", 0.9),
                      ("snare", 0.75), ("hat", 0.5), ("taiko", 0.85), ("cym", 0.5), ("lead", 1.0)], name, ir,
                  {"spic": 0.2, "brass": 0.3, "choir": 0.45, "pad": 0.3, "snare": 0.2, "taiko": 0.25, "lead": 0.35},
                  rms_db=-14.5)


def music_boss():
    # Chefe — Mi menor, 150 bpm, 24 compassos (38,4 s).
    progs = {"A": [CH(n) for n in ("Em", "C", "D", "B")], "B": [CH(n) for n in ("Am", "C", "B", "B")]}
    leads = {0: [(0, 0, 2, 76), (0, 2, 1, 79), (0, 3, 1, 78), (1, 0, 3, 76), (1, 3, 1, 74),
                 (2, 0, 2, 74), (2, 2, 1, 78), (2, 3, 1, 81), (3, 0, 4, 75)],
             1: [(0, 0, 1.5, 81), (0, 1.5, 0.5, 79), (0, 2, 2, 76), (1, 0, 2, 79), (1, 2, 2, 76),
                 (2, 0, 1, 75), (2, 1, 1, 76), (2, 2, 2, 78), (3, 0, 4, 71)]}
    return _boss_like("music_boss", 150, ["A", "A", "B", "A", "A", "B"], progs, leads, {1: 0, 2: 1, 4: 0, 5: 1}, 2, 1)


def music_boss_final():
    # Chefe final — Dó menor (dominante frígio), 168 bpm, 28 compassos (40 s). Tudo dobrado.
    progs = {"A": [CH(n) for n in ("Cm", "Ab", "Db", "G")], "B": [CH(n) for n in ("Fm", "Ab", "G", "G")]}
    leads = {0: [(0, 0, 1.5, 72), (0, 1.5, .5, 75), (0, 2, 2, 79), (1, 0, 1.5, 80), (1, 1.5, .5, 79), (1, 2, 2, 75),
                 (2, 0, 1, 77), (2, 1, 1, 80), (2, 2, 2, 73), (3, 0, 2, 74), (3, 2, 1, 71), (3, 3, 1, 67)],
             1: [(0, 0, 2, 77), (0, 2, 1, 80), (0, 3, 1, 84), (1, 0, 3, 80), (1, 3, 1, 79),
                 (2, 0, 2, 79), (2, 2, 1, 77), (2, 3, 1, 74), (3, 0, 4, 71)]}
    return _boss_like("music_boss_final", 168, ["A", "A", "B", "A", "B", "A", "B"], progs, leads,
                      {1: 0, 2: 1, 3: 0, 4: 1, 5: 0, 6: 1}, 1, 2)


def music_greed():
    # Ganância (modo futuro) — Sol menor harmônica, 128 bpm, 20 compassos (37,5 s).
    # Pizzicato, celesta dourada, relógio tiquetaqueando, moedas, cordas em trêmulo.
    L = Loop(128, 20)
    A = [CH(n) for n in ("Gm", "Eb", "Cm", "D")]
    Bp = [CH(n) for n in ("Gm", "Eb", "Am7b5", "D7")]
    sc = scale_notes(67, HARM, 74, 91)
    pats = [[.5, .5, .5, .5, 1, 1], [1, .5, .5, 1, 1], [.5, .5, 1, .5, .5, 1], [1.5, .5, 2]]
    melA = gen_melody(A, sc, 17, pats, cadence=(1, 1, 2))
    melB = gen_melody(Bp, sc, 27, pats, cadence=(2, 2))
    form = [(A, None), (A, melA), (Bp, melB), (A, melA), (Bp, melB)]
    bar = 4 * L.beat
    bar0 = 0
    for si, (prog, mel) in enumerate(form):
        if mel is not None:
            add_notes(L, "cel", mel, lambda m, d, **k: i_celesta(m, d + 0.8, **k), bar0, g=0.3)
            if prog is Bp:
                add_notes(L, "cel", mel, i_pizz, bar0, transpose=-12, g=0.25)
        for b, ch in enumerate(prog):
            br = bar0 + b
            vc = vchord(ch, 55)
            for e, k in enumerate([0, 2, 1, 2, 0, 2, 1, 2]):
                L.add("pizz", i_pizz(vc[k % len(vc)], 0.5, 0.35 if e % 2 == 0 else 0.25), L.at(br, e * 0.5))
            p = i_pad(vchord(ch, 62), bar, 0.35, att=0.2, rel=0.4, cutoff=1700)
            tt = np.arange(len(p)) / MSR
            L.add("trem", p * (0.6 + 0.4 * np.sin(2 * np.pi * (128 / 60 * 4) * tt)), L.at(br))
            for beat in range(4):
                L.add("bass", sine_note(bass_of(ch), L.beat * 0.5, att=0.005, rel=0.1, harm=0.35) * 0.5, L.at(br, beat))
                L.add("clock", wood(2200 if beat % 2 == 0 else 1650, 0.1, 0.8, MSR) * 0.12, L.at(br, beat))
            for e in range(16):
                L.add("clock", p_shaker(0.04 if e % 2 else 0.02, dur=0.06), L.at(br, e * 0.25))
            if b == 3:
                for j in range(4):
                    L.add("coin", lp(coin_clink(2400 + 300 * j, 0.5, 1.0, MSR), 7000, MSR) * 0.1, L.at(br, 3 + j * 0.12))
        bar0 += len(prog)
    ir = make_ir(1.4, damp=5000)
    return master(L, [("cel", 1.0), ("pizz", 0.9), ("trem", 0.6), ("bass", 0.8), ("clock", 0.8), ("coin", 0.8)],
                  "music_greed", ir, {"cel": 0.45, "pizz": 0.3, "trem": 0.4, "coin": 0.5}, rms_db=-17)


MUSIC = [music_menu, music_town, music_guild, music_forest, music_crypt, music_desert, music_snow,
         music_ruins_city, music_lava, music_boss, music_boss_final, music_greed]


def main():
    os.makedirs(OUT, exist_ok=True)
    GROUPS["sfx2"] = list(V2_IDS)                       # efeitos refeitos + novos
    GROUPS["music"] = [fn.__name__ for fn in MUSIC]     # todas as músicas
    only = set()
    for a in sys.argv[1:]:  # aceita ids soltos ou grupos: skills, ults, combo, sfx2, music, new
        if a == "new":
            for v in GROUPS.values():
                only.update(v)
        else:
            only.update(GROUPS.get(a, [a]))
    total = 0
    for sid in EFFECTS:
        if only and sid not in only:
            continue
        x, level = globals()["sfx_" + sid]()
        if sid in V2_IDS:
            x = normalize2(x, level)
            if sid not in LOOP_SFX:
                x = trim_tail(x)                     # loop: sem fade (emenda perfeita)
                x = fades(x, 0.001, 0.02)
        else:
            x = fades(normalize(x, level), 0.002, 0.015)
        p = write_wav(sid, x, SR)
        sz = os.path.getsize(p); total += sz
        print("%-18s %6.2f s %8.1f KB" % (sid, len(x) / SR, sz / 1024))
    for fn in MUSIC:
        name = fn.__name__
        if only and name not in only:
            continue
        p = fn()
        sz = os.path.getsize(p); total += sz
        with wave.open(p) as w:
            dur = w.getnframes() / w.getframerate()
        print("%-18s %6.2f s %8.1f KB" % (name, dur, sz / 1024))
    allsz = sum(os.path.getsize(os.path.join(OUT, f)) for f in os.listdir(OUT) if f.endswith(".wav"))
    print("Gerado nesta execução: %.2f MB | Total da pasta: %.2f MB" % (total / 1048576, allsz / 1048576))


if __name__ == "__main__":
    main()
