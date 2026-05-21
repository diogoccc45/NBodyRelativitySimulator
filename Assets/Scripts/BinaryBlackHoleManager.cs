using UnityEngine;
using UnityEngine.UI;
using System.Collections;
public class BinaryBlackHoleManager : MonoBehaviour
{
    [Header("Referências")]
    public RelativityManager       relativityManager;
    public GravitationalWaves      gravitationalWaves;
    public SpacetimeGrid           grid;
    public RelativityCameraManager cameraManager;

    [Header("Câmaras")]
    public OrbitalCamera orbitalCamera;
    public CameraFly     flyCamera;

    [Header("Modo Cinematográfico")]
    public MeshRenderer gridRenderer;
    public Canvas       uiCanvas;
    public float cinematicDistanceRatio = 0.85f;
    public float cinematicMinDistance   = 15f;
    public float cinematicMaxDistance   = 120f;
    public float cinematicHeight        = 4f;
    public float cinematicSmoothSpeed   = 3f;

    [Header("UI")]
    public Button scatteringButton;

    [Header("Física do Scattering")]
    public float G                   = 80f;
    public float initialOrbitalSpeed = 12f;
    public float activationRadius    = 80f;
    public float velocityDamping     = 0.98f;

    [Header("Ondas Gravitacionais")]
    public float waveInterval = 0.4f;

    [Header("Deformação Tidal 3PM")]
    [Tooltip("Distância a partir da qual a deformação começa — ajusta à separação inicial dos teus BHs")]
    public float tidalStartDistance = 60f;
    [Tooltip("Distância de closest approach — intensidade máxima aqui")]
    public float tidalPeakDistance  = 8f;
    [Tooltip("Multiplicador global — aumenta se não vires deformação (começa em 1, sobe até 3 se necessário)")]
    public float tidalMagnitude     = 1.0f;

    // Debug — visível no Inspector durante play mode
    [Header("Debug (só leitura)")]
    [SerializeField] private float  dbg_dist       = 0f;
    [SerializeField] private float  dbg_tidalStr   = 0f;
    [SerializeField] private string dbg_matA       = "—";
    [SerializeField] private string dbg_matAF      = "—";
    [SerializeField] private string dbg_matB       = "—";
    [SerializeField] private string dbg_matBF      = "—";

    // Estado interno
    private BlackHoleBody bhA, bhB;
    private bool isBinaryActive, isCinematicActive, scatteringStarted;
    private Vector3 velA, velB;
    private float waveTimer, minDistReached = float.MaxValue;
    private bool separating;
    private RelativityCameraManager.CameraMode previousCamMode;

    // Câmara
    private Vector3 cinematicTargetPos, cinematicCurrentPos;
    private bool cinematicPosInitialized;

    // Materiais instanciados — criados uma vez, reutilizados sempre
    private Material matDiskA, matDiskFrontA, matDiskB, matDiskFrontB;
    private bool matsInstanced;

    // ─────────────────────────────────────────────────────────────────────
    void Start()
    {
        if (scatteringButton != null) scatteringButton.gameObject.SetActive(false);
    }

    void Update()
    {
        DetectBlackHoles();
        if (!isBinaryActive || !scatteringStarted) return;
        UpdateScatteringPhysics();
        UpdateWaves();
        CheckSeparation();
    }

    void LateUpdate()
    {
        if (isCinematicActive && scatteringStarted) SmoothCinematicCamera();
    }

    // ─────────────────────────────────────────────────────────────────────
    // DETEÇÃO
    void DetectBlackHoles()
    {
        var allBHs = FindObjectsByType<BlackHoleBody>(FindObjectsSortMode.None);
        if (allBHs.Length == 2)
        {
            if (isBinaryActive && ((bhA == allBHs[0] && bhB == allBHs[1]) || (bhA == allBHs[1] && bhB == allBHs[0]))) return;
            bhA = allBHs[0]; bhB = allBHs[1];
            isBinaryActive = true; scatteringStarted = false; matsInstanced = false;
            if (scatteringButton != null) scatteringButton.gameObject.SetActive(true);
        }
        else
        {
            if (isBinaryActive) DeactivateBinaryMode();
            if (scatteringButton != null) scatteringButton.gameObject.SetActive(false);
        }
    }

    // ─────────────────────────────────────────────────────────────────────
    // BOTÃO
    public void OnScatteringButtonPressed()
    {
        if (bhA == null || bhB == null || !isBinaryActive) return;
        if (scatteringButton != null) scatteringButton.gameObject.SetActive(false);

        InstanciateDiskMaterials();

        ActivateCinematicCamera();

        var rbA = bhA.GetComponent<RelativityBody>();
        var rbB = bhB.GetComponent<RelativityBody>();
        if (rbA != null) rbA.enabled = false;
        if (rbB != null) rbB.enabled = false;

        if (relativityManager != null)
        {
            var col = relativityManager.GetComponent<RelativityCollision>();
            if (col != null) col.enabled = false;
        }

        Vector3 sep   = (bhB.transform.position - bhA.transform.position); sep.y = 0f;
        Vector3 radial = sep.normalized;
        velA =  radial * initialOrbitalSpeed;
        velB = -radial * initialOrbitalSpeed;
        minDistReached = sep.magnitude; separating = false; scatteringStarted = true;
    }

    // ─────────────────────────────────────────────────────────────────────
    // INSTANCIAÇÃO DE MATERIAIS
    // Uma instância por disco, criada uma vez. Resolve o problema de
    // Renderer.material criar uma cópia nova e descartada a cada acesso.
    void InstanciateDiskMaterials()
    {
        if (matsInstanced) return;

        matDiskA      = InstMat(bhA?.accretionDisk,      bhA, "Disk-A");
        matDiskFrontA = InstMat(bhA?.accretionDiskFront, bhA, "DiskFront-A");
        matDiskB      = InstMat(bhB?.accretionDisk,      bhB, "Disk-B");
        matDiskFrontB = InstMat(bhB?.accretionDiskFront, bhB, "DiskFront-B");

        dbg_matA  = matDiskA      != null ? matDiskA.shader.name      : "NULL — accretionDisk do BH-A não encontrado";
        dbg_matAF = matDiskFrontA != null ? matDiskFrontA.shader.name : "NULL — accretionDiskFront do BH-A não encontrado";
        dbg_matB  = matDiskB      != null ? matDiskB.shader.name      : "NULL — accretionDisk do BH-B não encontrado";
        dbg_matBF = matDiskFrontB != null ? matDiskFrontB.shader.name : "NULL — accretionDiskFront do BH-B não encontrado";

        // Verifica se as props tidal existem no shader
        CheckTidalProps(matDiskA, "Disk-A");

        matsInstanced = true;
    }

    Material InstMat(GameObject diskObj, BlackHoleBody bh, string label)
    {
        if (diskObj == null)
        {
            Debug.LogWarning($"[Tidal] {label}: GameObject é null. Verifica o campo no Inspector de '{bh?.name}'.");
            return null;
        }
        var r = diskObj.GetComponent<Renderer>();
        if (r == null) { Debug.LogWarning($"[Tidal] {label} '{diskObj.name}': sem Renderer."); return null; }
        if (r.sharedMaterial == null) { Debug.LogWarning($"[Tidal] {label} '{diskObj.name}': sharedMaterial é null."); return null; }

        var inst  = new Material(r.sharedMaterial);
        inst.name = r.sharedMaterial.name + "_tidal_" + label;
        r.material = inst; // aplica a instância ao renderer
        return inst;
    }

    void CheckTidalProps(Material mat, string label)
    {
        if (mat == null) return;
        string[] props = { "_TidalStretch", "_TidalSquish", "_TidalAngle", "_TidalInnerPush", "_HotSpotAngle", "_HotSpotStr", "_DopplerBias" };
        foreach (var p in props)
        {
            if (!mat.HasProperty(p))
                Debug.LogWarning($"[Tidal] {label}: shader '{mat.shader.name}' NÃO tem '{p}'. Confirma que o shader novo está guardado e atribuído ao material no projeto.");
        }
        Debug.Log($"[Tidal] {label}: shader='{mat.shader.name}' — props tidal verificadas.");
    }

    // ─────────────────────────────────────────────────────────────────────
    // FÍSICA
    void UpdateScatteringPhysics()
    {
        if (bhA == null || bhB == null) return;

        Vector3 posA = bhA.transform.position, posB = bhB.transform.position;
        Vector3 dir  = posB - posA; dir.y = 0f;
        float   dist = Mathf.Max(dir.magnitude, 5f);

        var rbA = bhA.GetComponent<RelativityBody>(); float mA = rbA != null ? rbA.mass : 800f;
        var rbB = bhB.GetComponent<RelativityBody>(); float mB = rbB != null ? rbB.mass : 800f;

        float   fmag = G * mA * mB / (dist * dist);
        Vector3 fdir = dir.normalized;
        velA += fdir * (fmag / mA) * Time.deltaTime;
        velB -= fdir * (fmag / mB) * Time.deltaTime;
        float damp = Mathf.Pow(velocityDamping, Time.deltaTime * 60f);
        velA *= damp; velB *= damp;

        bhA.transform.position = new Vector3(posA.x + velA.x * Time.deltaTime, posA.y, posA.z + velA.z * Time.deltaTime);
        bhB.transform.position = new Vector3(posB.x + velB.x * Time.deltaTime, posB.y, posB.z + velB.z * Time.deltaTime);

        float proximity = Mathf.Clamp01(1f - dist / 80f);
        UpdateDiskBrightness(proximity);
        TiltDisksTowardEachOther(proximity);
        UpdateTidalDeformation(dist, dir.normalized, mA, mB);
        UpdateCinematicTarget(dist, dir);
    }

    // ─────────────────────────────────────────────────────────────────────
    // DEFORMAÇÃO TIDAL 3PM
    //
    // O shader opera em coordenadas UV (espaço local do quad).
    // O ângulo tidal tem de ser calculado no espaço local de cada quad:
    //   1. Transforma dirAtoB para o espaço local do transform do disco
    //   2. Usa as componentes X e Y (que correspondem a U e V no quad)
    //   3. Calcula Atan2 nesse referencial
    //
    // Sem esta conversão o ângulo está no espaço do mundo e o shader
    // aplica a deformação na direção errada — não há deformação visível.
    void UpdateTidalDeformation(float dist, Vector3 dirAtoB, float mA, float mB)
    {
        if (!matsInstanced) return;

        // Intensidade tidal — smoothstep entre tidalStartDistance e tidalPeakDistance
        float t = 1f - Mathf.Clamp01((dist - tidalPeakDistance) / Mathf.Max(tidalStartDistance - tidalPeakDistance, 1f));
        t = t * t * (3f - 2f * t); // smootherstep

        // Escala linear adicional com a proximidade — para ser visível mais cedo
        float tidalStr = t * tidalMagnitude;
        tidalStr = Mathf.Clamp01(tidalStr);

        dbg_dist     = dist;
        dbg_tidalStr = tidalStr;

        float massRatio = Mathf.Clamp(mA / Mathf.Max(mB, 1f), 0.2f, 5f);

        // BH-A — deformado pelo campo de B; BH-B — deformado pelo campo de A (escala pela razão de massas)
        ApplyTidalToBH(bhA, matDiskA, matDiskFrontA,  dirAtoB,           tidalStr,                velB);
        ApplyTidalToBH(bhB, matDiskB, matDiskFrontB, -dirAtoB, tidalStr * massRatio, velA);
    }

    void ApplyTidalToBH(BlackHoleBody bh, Material matDisk, Material matFront,
                        Vector3 dirToCompanion, float tidalStr, Vector3 companionVel)
    {
        if (bh == null) return;

        // ── Ângulo no espaço local do disco ───────────────────────────
        // O quad usa X=right, Y=up no seu espaço local (que mapeia para U,V no shader).
        // Transforma dirToCompanion para o espaço local do transform do disco.
        Vector3 localDirDisk  = GetLocalAngle(bh.accretionDisk,      dirToCompanion);
        Vector3 localDirFront = GetLocalAngle(bh.accretionDiskFront, dirToCompanion);

        float angleDisk  = Mathf.Atan2(localDirDisk.y,  localDirDisk.x);
        float angleFront = Mathf.Atan2(localDirFront.y, localDirFront.x);

        // Hot spot 90° do eixo de separação — ponto de máxima compressão tidal
        float hotDisk  = angleDisk  + Mathf.PI * 0.5f;
        float hotFront = angleFront + Mathf.PI * 0.5f;

        // Viés Doppler — componente da velocidade do companheiro ao longo do eixo de separação
        float dopplerBias = Mathf.Clamp(Vector3.Dot(companionVel.normalized, dirToCompanion) * tidalStr, -1f, 1f);

        SetTidal(matDisk,  tidalStr, tidalStr * 0.75f, angleDisk,  tidalStr * 0.12f, hotDisk,  tidalStr * 2f, dopplerBias);
        SetTidal(matFront, tidalStr, tidalStr * 0.75f, angleFront, tidalStr * 0.12f, hotFront, tidalStr * 2f, dopplerBias);
    }

    // Transforma dirToCompanion (mundo) para o espaço local do transform do quad
    // Devolve as componentes X e Y locais — que correspondem a U e V no shader
    Vector3 GetLocalAngle(GameObject diskObj, Vector3 worldDir)
    {
        if (diskObj == null) return Vector3.right;
        // InverseTransformDirection ignora posição e escala — só rotação
        return diskObj.transform.InverseTransformDirection(worldDir).normalized;
    }

    void SetTidal(Material mat, float stretch, float squish, float angle,
                  float innerPush, float hotAngle, float hotStr, float dopplerBias)
    {
        if (mat == null) return;
        mat.SetFloat("_TidalStretch",   stretch);
        mat.SetFloat("_TidalSquish",    squish);
        mat.SetFloat("_TidalAngle",     angle);
        mat.SetFloat("_TidalInnerPush", innerPush);
        mat.SetFloat("_HotSpotAngle",   hotAngle);
        mat.SetFloat("_HotSpotStr",     hotStr);
        mat.SetFloat("_DopplerBias",    dopplerBias);
    }

    void ResetTidal()
    {
        foreach (var m in new[] { matDiskA, matDiskFrontA, matDiskB, matDiskFrontB })
            SetTidal(m, 0f, 0f, 0f, 0f, 0f, 0f, 0f);
    }

    // ─────────────────────────────────────────────────────────────────────
    // CÂMARA CINEMATOGRÁFICA ADAPTATIVA
    void ActivateCinematicCamera()
    {
        isCinematicActive = true; cinematicPosInitialized = false;
        if (cameraManager != null) { previousCamMode = cameraManager.CurrentMode; cameraManager.enabled = false; }
        if (orbitalCamera != null) orbitalCamera.enabled = false;
        if (flyCamera     != null) flyCamera.enabled     = false;
        if (gridRenderer  != null) gridRenderer.enabled  = false;
        if (uiCanvas      != null) uiCanvas.enabled      = false;
        SnapCinematicCamera();
    }

    void SnapCinematicCamera()
    {
        if (bhA == null || bhB == null) return;
        Vector3 center = (bhA.transform.position + bhB.transform.position) * 0.5f;
        float   dist   = Vector3.Distance(bhA.transform.position, bhB.transform.position);
        Vector3 sep    = bhB.transform.position - bhA.transform.position; sep.y = 0f;
        Vector3 perp   = sep.sqrMagnitude > 0.01f ? Vector3.Cross(Vector3.up, sep.normalized).normalized : Vector3.forward;
        float   camD   = Mathf.Clamp(dist * cinematicDistanceRatio, cinematicMinDistance, cinematicMaxDistance);
        Vector3 pos    = center + perp * camD + Vector3.up * cinematicHeight;
        var cam = Camera.main;
        if (cam != null) { cam.transform.position = pos; cam.transform.LookAt(center); }
        cinematicCurrentPos = pos; cinematicTargetPos = pos; cinematicPosInitialized = true;
    }

    void UpdateCinematicTarget(float dist, Vector3 sepDir)
    {
        if (bhA == null || bhB == null) return;
        Vector3 center = (bhA.transform.position + bhB.transform.position) * 0.5f;
        Vector3 perp   = sepDir.sqrMagnitude > 0.01f ? Vector3.Cross(Vector3.up, sepDir.normalized).normalized : Vector3.forward;
        float   camD   = Mathf.Clamp(dist * cinematicDistanceRatio, cinematicMinDistance, cinematicMaxDistance);
        cinematicTargetPos = center + perp * camD + Vector3.up * cinematicHeight;
    }

    void SmoothCinematicCamera()
    {
        if (!cinematicPosInitialized) return;
        var cam = Camera.main; if (cam == null) return;
        cinematicCurrentPos = Vector3.Lerp(cinematicCurrentPos, cinematicTargetPos, Time.deltaTime * cinematicSmoothSpeed);
        cam.transform.position = cinematicCurrentPos;
        if (bhA != null && bhB != null)
            cam.transform.LookAt((bhA.transform.position + bhB.transform.position) * 0.5f);
    }

    // ─────────────────────────────────────────────────────────────────────
    // EFEITOS VISUAIS BASE
    void UpdateDiskBrightness(float proximity)
    {
        float b = Mathf.Lerp(1f, 5f, proximity);
        foreach (var m in new[] { matDiskA, matDiskFrontA, matDiskB, matDiskFrontB })
            if (m != null && m.HasProperty("_Brightness")) m.SetFloat("_Brightness", b);
    }

    void TiltDisksTowardEachOther(float proximity)
    {
        if (bhA == null || bhB == null) return;
        Vector3 d = (bhB.transform.position - bhA.transform.position).normalized;
        TiltDisk(bhA,  d, proximity);
        TiltDisk(bhB, -d, proximity);
    }

    void TiltDisk(BlackHoleBody bh, Vector3 dir, float proximity)
    {
        if (bh == null) return;
        Quaternion baseRot = Quaternion.Euler(bh.diskTilt, 0f, 0f);
        Quaternion toward  = Quaternion.FromToRotation(Vector3.up, dir);
        Quaternion final   = Quaternion.Slerp(baseRot, toward, proximity * 0.5f);
        if (bh.accretionDisk      != null) bh.accretionDisk.transform.localRotation      = final;
        if (bh.accretionDiskFront != null) bh.accretionDiskFront.transform.localRotation = final;
    }

    // ─────────────────────────────────────────────────────────────────────
    // ONDAS
    void UpdateWaves()
    {
        if (gravitationalWaves == null || bhA == null || bhB == null) return;
        waveTimer += Time.deltaTime;
        float dist  = Vector3.Distance(bhA.transform.position, bhB.transform.position);
        float prox  = Mathf.Clamp01(1f - dist / activationRadius);
        float intv  = Mathf.Lerp(waveInterval, waveInterval * 0.15f, prox);
        if (waveTimer >= intv)
        {
            waveTimer = 0f;
            gravitationalWaves.SpawnWave((bhA.transform.position + bhB.transform.position) * 0.5f, prox * 1000f);
        }
    }

    void CheckSeparation()
    {
        if (bhA == null || bhB == null) return;
        float dist = Vector3.Distance(bhA.transform.position, bhB.transform.position);
        if (dist < minDistReached) minDistReached = dist;
        if (!separating && dist > minDistReached * 1.3f) { separating = true; OnBlackHolesPassedEachOther(); }
        float gh = grid != null ? grid.GridWorldSize * 0.5f : 100f;
        if (Mathf.Abs(bhA.transform.position.x) > gh * 1.5f) DeactivateBinaryMode();
    }

    void OnBlackHolesPassedEachOther()
    {
        if (gravitationalWaves != null)
        {
            Vector3 c = (bhA.transform.position + bhB.transform.position) * 0.5f;
            gravitationalWaves.SpawnShockwave(c, 2000f);
            StartCoroutine(DelayedShockwave(c, 1000f, 0.3f));
            StartCoroutine(DelayedShockwave(c,  500f, 0.7f));
        }
        StartCoroutine(FadeTidal(1.5f));
        if (isCinematicActive) StartCoroutine(ReturnToNormalCamera(3f));
    }

    IEnumerator FadeTidal(float dur)
    {
        float e = 0f;
        while (e < dur)
        {
            e += Time.deltaTime;
            float fade = Mathf.Pow(1f - e / dur, 2f);
            foreach (var m in new[] { matDiskA, matDiskFrontA, matDiskB, matDiskFrontB })
            {
                if (m == null) continue;
                m.SetFloat("_TidalStretch",   m.GetFloat("_TidalStretch")   * fade);
                m.SetFloat("_TidalSquish",    m.GetFloat("_TidalSquish")    * fade);
                m.SetFloat("_TidalInnerPush", m.GetFloat("_TidalInnerPush") * fade);
                m.SetFloat("_HotSpotStr",     m.GetFloat("_HotSpotStr")     * fade);
            }
            yield return null;
        }
        ResetTidal();
    }

    // ─────────────────────────────────────────────────────────────────────
    // CLEANUP
    IEnumerator ReturnToNormalCamera(float delay)
    {
        yield return new WaitForSeconds(delay);
        isCinematicActive = false; scatteringStarted = false;
        if (gridRenderer  != null) gridRenderer.enabled  = true;
        if (uiCanvas      != null) uiCanvas.enabled      = true;
        if (cameraManager != null) { cameraManager.enabled = true; cameraManager.SetMode(previousCamMode); }
        if (relativityManager != null) { var col = relativityManager.GetComponent<RelativityCollision>(); if (col != null) col.enabled = true; }
        if (previousCamMode == RelativityCameraManager.CameraMode.Orbital && orbitalCamera != null) orbitalCamera.enabled = true;
        else if (previousCamMode == RelativityCameraManager.CameraMode.Fly && flyCamera != null) flyCamera.enabled = true;
        if (bhA != null) { var rb = bhA.GetComponent<RelativityBody>(); if (rb != null) rb.enabled = true; }
        if (bhB != null) { var rb = bhB.GetComponent<RelativityBody>(); if (rb != null) rb.enabled = true; }
        isBinaryActive = false;
    }

    void DeactivateBinaryMode()
    {
        ResetTidal();
        if (bhA != null) { var rb = bhA.GetComponent<RelativityBody>(); if (rb != null) rb.enabled = true; }
        if (bhB != null) { var rb = bhB.GetComponent<RelativityBody>(); if (rb != null) rb.enabled = true; }
        isBinaryActive = isCinematicActive = scatteringStarted = matsInstanced = false;
        matDiskA = matDiskFrontA = matDiskB = matDiskFrontB = null;
        bhA = bhB = null; minDistReached = float.MaxValue; separating = false;
        if (gridRenderer  != null) gridRenderer.enabled  = true;
        if (uiCanvas      != null) uiCanvas.enabled      = true;
        if (cameraManager != null) cameraManager.enabled = true;
        if (scatteringButton != null) scatteringButton.gameObject.SetActive(false);
    }

    IEnumerator DelayedShockwave(Vector3 pos, float mass, float delay)
    {
        yield return new WaitForSeconds(delay);
        if (gravitationalWaves != null) gravitationalWaves.SpawnShockwave(pos, mass);
    }
}