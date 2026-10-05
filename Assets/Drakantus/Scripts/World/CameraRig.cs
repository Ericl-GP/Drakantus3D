using UnityEngine;

namespace Drakantus
{
    /// <summary>
    /// Câmera do jogo com zoom contínuo entre a vista ISOMÉTRICA (zoom01 = 0: longe, pitch 55°, yaw fixo 45°)
    /// e a 3ª PESSOA (zoom01 = 1: perto, câmera baixa atrás do herói, yaw seguindo a direção dele).
    /// Rodinha do mouse muda o zoom (salvo em PlayerPrefs "cam_zoom"). Tudo é suavizado; em zoom
    /// alto a câmera evita atravessar paredes (SphereCast do alvo até a câmera). Tremor por "trauma"
    /// (ruído Perlin) e "Punch" (zoom-in rápido cinematográfico). Atualiza em LateUpdate.
    /// Adiciona sozinha o OcclusionFader (deixa transparentes árvores/casas entre câmera e herói).
    /// </summary>
    [RequireComponent(typeof(Camera))]
    public class CameraRig : MonoBehaviour
    {
        public static Camera Cam;
        public static CameraRig I;

        const string PrefKey = "cam_zoom";

        public Transform target;

        [Header("Isométrica (zoom01 = 0)")]
        public float farDistance = 24f;
        public float farPitch = 55f;
        public float farFov = 32f;
        public float farLift = 0.8f;      // altura do ponto observado (pé)
        public float isoYaw = 45f;

        [Header("3ª pessoa (zoom01 = 1)")]
        public float nearDistance = 4.5f;
        public float nearPitch = 14f;
        public float nearFov = 55f;
        public float nearLift = 1.7f;     // ombro
        public float followYawFrom = 0.7f; // acima disso o yaw segue o herói

        [Header("Controle")]
        public float zoomStep = 0.1f;     // por "clique" da rodinha
        public float defaultZoom = 0.2f;  // ≈ a vista antiga (distância ~17 m)
        public float followTime = 0.12f;
        public float collisionRadius = 0.3f;

        // ---- estado atual (somente leitura para os outros módulos)
        public float pitch = 55f, yaw = 45f, fov = 32f, distance = 24f;
        /// <summary>Zoom atual suavizado: 0 = isométrica longe, 1 = 3ª pessoa.</summary>
        public float zoom01 => zoomCur;
        /// <summary>Zoom pedido (para onde o zoom atual está indo).</summary>
        public float zoomTarget => zoomGoal;

        float zoomCur, zoomGoal, zoomVel;
        float yawVel, followYaw;
        bool following;
        float trauma, seed, scrollCd;
        float punch, punchGoal, punchHold, punchDecay;
        float pull;                         // quanto a colisão encurtou a distância
        float lift = 0.8f;
        Vector3 focus, focusVel;
        readonly RaycastHit[] castBuf = new RaycastHit[32];

        void Awake()
        {
            I = this;
            Cam = GetComponent<Camera>();
            seed = Random.value * 100f;
            zoomGoal = zoomCur = Mathf.Clamp01(PlayerPrefs.GetFloat(PrefKey, defaultZoom));
            yaw = isoYaw;
            ComputeParams();
            Cam.fieldOfView = fov;
            if (GetComponent<OcclusionFader>() == null) gameObject.AddComponent<OcclusionFader>();
        }

        void OnDestroy()
        {
            PlayerPrefs.SetFloat(PrefKey, zoomGoal);
            PlayerPrefs.Save();
            if (I == this) I = null;
        }

        void OnApplicationQuit()
        {
            PlayerPrefs.SetFloat(PrefKey, zoomGoal);
            PlayerPrefs.Save();
        }

        /// <summary>Converte o input de tela (WASD) numa direção no chão relativa à câmera (usa o yaw ATUAL).</summary>
        public static Vector3 ToWorld(Vector2 input)
        {
            float y = I != null ? I.yaw : 45f;
            Vector3 v = Quaternion.Euler(0f, y, 0f) * new Vector3(input.x, 0f, input.y);
            v.y = 0f;
            if (v.sqrMagnitude > 1f) v.Normalize();
            return v;
        }

        /// <summary>Posiciona a câmera no alvo imediatamente (sem suavização).</summary>
        public void Snap()
        {
            zoomCur = zoomGoal;
            zoomVel = 0f;
            punch = punchGoal = punchHold = 0f;
            ComputeParams();
            following = false;
            yaw = YawGoal(true);
            yawVel = 0f;
            if (target != null) focus = target.position + Vector3.up * lift;
            focusVel = Vector3.zero;
            pull = 0f;
            Apply(0f, true);
        }

        /// <summary>Aponta a câmera para um ponto fixo (tela de título).</summary>
        public void Focus(Vector3 point)
        {
            target = null;
            ComputeParams();
            focus = point + Vector3.up * lift;
            focusVel = Vector3.zero;
            yaw = isoYaw; yawVel = 0f;
            pull = 0f;
            Apply(0f, true);
        }

        public void AddTrauma(float t)
        {
            trauma = Mathf.Clamp01(trauma + Mathf.Max(0f, t));
        }

        /// <summary>
        /// Zoom-in rápido cinematográfico (ultimates/golpes fortes). amount 0..1 (1 = aproxima ~30%).
        /// Sobe em ~0,08 s e volta ao normal em ~0,5 s.
        /// </summary>
        public void Punch(float amount)
        {
            amount = Mathf.Clamp01(amount);
            if (amount <= 0f) return;
            punchGoal = Mathf.Max(punchGoal, amount);
            punchHold = 0.08f;
            punchDecay = punchGoal / 0.42f;
        }

        /// <summary>Define o zoom (0 = isométrica, 1 = 3ª pessoa) e salva a preferência.</summary>
        public void SetZoom(float z01, bool instant = false)
        {
            zoomGoal = Mathf.Clamp01(z01);
            PlayerPrefs.SetFloat(PrefKey, zoomGoal);
            if (instant) { zoomCur = zoomGoal; zoomVel = 0f; }
        }

        void LateUpdate()
        {
            float dt = Time.deltaTime;
            float udt = Time.unscaledDeltaTime;

            ReadZoomInput(udt);
            zoomCur = Mathf.SmoothDamp(zoomCur, zoomGoal, ref zoomVel, 0.18f, Mathf.Infinity, udt);

            // punch: segura um instante e decai linearmente; o valor visível persegue rápido
            if (punchHold > 0f) punchHold -= udt;
            else punchGoal = Mathf.MoveTowards(punchGoal, 0f, punchDecay * udt);
            punch = Mathf.Lerp(punch, punchGoal, 1f - Mathf.Exp(-30f * udt));

            ComputeParams();

            float yGoal = YawGoal(false);
            yaw = Mathf.SmoothDampAngle(yaw, yGoal, ref yawVel, following ? 0.45f : 0.3f, Mathf.Infinity, udt);

            if (target != null)
            {
                float s = Smooth(zoomCur);
                float ft = Mathf.Lerp(followTime, 0.05f, s);
                Vector3 goal = target.position + Vector3.up * lift;
                focus = Vector3.SmoothDamp(focus, goal, ref focusVel, ft, Mathf.Infinity, dt);
            }

            trauma = Mathf.Max(0f, trauma - udt * 1.4f);
            Apply(udt, false);
        }

        // ------------------------------------------------------------------ zoom
        void ReadZoomInput(float udt)
        {
            if (scrollCd > 0f) scrollCd -= udt;
            if (target == null) return;
            if (HUD.PointerOverUI() || (HUD.I != null && HUD.I.HasModal)) return;
            // O Input System novo pode entregar ~120 por clique (ou ~1, ou 1/120 depois do /120
            // feito no InputW, conforme a versão/plataforma). Por isso usamos só o SINAL por evento.
            float sc = InputW.Scroll;
            if (Mathf.Abs(sc) < 0.0001f || scrollCd > 0f) return;
            scrollCd = 0.035f;   // touchpads mandam eventos minúsculos todo frame: limita a ~30 passos/s
            SetZoom(zoomGoal + Mathf.Sign(sc) * zoomStep);
            // TODO(contrato): teclas +/- ou PageUp/PageDown não existem no InputW (enum K); quando o líder
            // adicionar, chamar SetZoom(zoomGoal ± zoomStep) aqui.
        }

        static float Smooth(float t) { t = Mathf.Clamp01(t); return t * t * (3f - 2f * t); }

        void ComputeParams()
        {
            float z = Mathf.Clamp01(zoomCur);
            float s = Smooth(z);
            pitch = Mathf.Lerp(farPitch, nearPitch, s);
            fov = Mathf.Lerp(farFov, nearFov, s);
            lift = Mathf.Lerp(farLift, nearLift, s);
            // interpolação exponencial: cada clique da rodinha "parece" igual
            distance = farDistance * Mathf.Pow(nearDistance / farDistance, z);
        }

        /// <summary>Yaw desejado: 45° fixo; acima de followYawFrom segue a direção do herói (atrás dele).</summary>
        float YawGoal(bool snap)
        {
            Player p = Game.I != null ? Game.I.player : null;
            bool canFollow = target != null && p != null && p.transform == target && zoomCur > followYawFrom;
            if (!canFollow) { following = false; return isoYaw; }

            Vector3 f = p.facing; f.y = 0f;
            bool hasFacing = f.sqrMagnitude > 0.01f;
            float fy = hasFacing ? Mathf.Atan2(f.x, f.z) * Mathf.Rad2Deg : yaw;
            if (!following || snap)
            {
                // ao entrar na 3ª pessoa: vai para trás do herói
                following = true;
                followYaw = fy;
            }
            else if (hasFacing && p.state == "run")
            {
                // só segue enquanto corre e se a direção não for "para a câmera"
                // (evita a câmera girar 180° quando o jogador anda para trás)
                if (Mathf.Abs(Mathf.DeltaAngle(followYaw, fy)) < 110f)
                    followYaw = Mathf.MoveTowardsAngle(followYaw, fy, 140f * Time.unscaledDeltaTime);
            }
            return followYaw;
        }

        // ------------------------------------------------------------------ posição final
        void Apply(float udt, bool snap)
        {
            Quaternion rot = Quaternion.Euler(pitch, yaw, 0f);
            Vector3 back = rot * Vector3.back;
            float want = distance * (1f - 0.3f * punch);

            // colisão: só quando aproximado (na isométrica o OcclusionFader resolve)
            float allowed = want;
            if (target != null && zoomCur > 0.45f) allowed = CastDistance(focus, back, want, zoomCur < 0.75f);
            float goalPull = Mathf.Max(0f, want - allowed);
            if (snap || goalPull > pull) pull = goalPull;                       // aproxima na hora
            else pull = Mathf.Lerp(pull, goalPull, 1f - Mathf.Exp(-4f * udt)); // afasta devagar
            float d = Mathf.Max(0.6f, want - pull);

            Vector3 pos = focus + back * d;
            if (trauma > 0f)
            {
                float s = trauma * trauma;
                float t = Time.unscaledTime * 22f;
                float ox = (Mathf.PerlinNoise(seed, t) - 0.5f) * 2f;
                float oy = (Mathf.PerlinNoise(seed + 10f, t) - 0.5f) * 2f;
                float roll = (Mathf.PerlinNoise(seed + 20f, t) - 0.5f) * 2f;
                float amp = Mathf.Lerp(0.45f, 0.18f, Smooth(zoomCur));   // perto treme menos em metros
                pos += rot * new Vector3(ox, oy, 0f) * amp * s;
                rot = rot * Quaternion.Euler(oy * 1.2f * s, ox * 1.2f * s, roll * 3f * s);
            }
            transform.SetPositionAndRotation(pos, rot);
            if (Cam != null) Cam.fieldOfView = Mathf.Clamp(fov - 6f * punch, 15f, 80f);
        }

        /// <summary>
        /// Distância livre do alvo até a câmera (SphereCast). Ignora o herói, inimigos, NPCs e gatilhos.
        /// ignoreOccluders: em zoom médio, árvores/casas ficam transparentes em vez de puxar a câmera.
        /// </summary>
        float CastDistance(Vector3 origin, Vector3 dir, float maxDist, bool ignoreOccluders)
        {
            int n = Physics.SphereCastNonAlloc(origin, collisionRadius, dir, castBuf, maxDist, ~0, QueryTriggerInteraction.Ignore);
            float best = maxDist;
            for (int i = 0; i < n; i++)
            {
                var h = castBuf[i];
                var c = h.collider;
                if (c == null || h.distance <= 0f) continue;   // distância 0 = já começou encostado (ex.: o próprio herói)
                if (h.distance - 0.15f >= best) continue;
                if (c.GetComponentInParent<Player>() != null) continue;
                if (c.GetComponentInParent<Enemy>() != null) continue;
                if (c.GetComponentInParent<Interactable>() != null) continue;
                if (ignoreOccluders)
                {
                    if (c.GetComponentInParent<Occluder>() != null) continue;
                    // colisores invisíveis (mata fechada, muralha, troncos) também: o fader mostra o que há atrás
                    if (c.GetComponent<Renderer>() == null) continue;
                }
                best = Mathf.Max(0f, h.distance - 0.15f);
            }
            return best;
        }
    }
}
