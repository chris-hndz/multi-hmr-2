using UnityEngine;
using Newtonsoft.Json;

public class SMPLXAligner : MonoBehaviour
{
    public string jsonFilePath;
    public GameObject smplxPrefab;
    public Camera alignmentCamera;
    public Texture2D backgroundImage;

    private SMPLXParams parameters;
    private string[] _smplxJointNames = new string[] { "pelvis", "left_hip", "right_hip", "spine1", "left_knee", "right_knee", "spine2", "left_ankle", "right_ankle", "spine3", "left_foot", "right_foot", "neck", "left_collar", "right_collar", "head", "left_shoulder", "right_shoulder", "left_elbow", "right_elbow", "left_wrist", "right_wrist", "left_index1", "left_index2", "left_index3", "left_middle1", "left_middle2", "left_middle3", "left_pinky1", "left_pinky2", "left_pinky3", "left_ring1", "left_ring2", "left_ring3", "left_thumb1", "left_thumb2", "left_thumb3", "right_index1", "right_index2", "right_index3", "right_middle1", "right_middle2", "right_middle3", "right_pinky1", "right_pinky2", "right_pinky3", "right_ring1", "right_ring2", "right_ring3", "right_thumb1", "right_thumb2", "right_thumb3", "jaw"};

    void Start()
    {
        parameters = JSONReader.ReadJSONFile(jsonFilePath);
        if (parameters != null)
        {
            SetupCamera();
            AlignSMPLX();
            SetupBackgroundImage();
        }
    }

    void SetupCamera()
    {
        Debug.Log("parameters.camera_intrinsics.Length = " + parameters.camera_intrinsics.Length);
        
        float fx = parameters.camera_intrinsics[0][0];
        float fy = parameters.camera_intrinsics[1][1];
        float cx = parameters.camera_intrinsics[0][2];
        float cy = parameters.camera_intrinsics[1][2];

        float fovY = 2 * Mathf.Atan(parameters.image_height / (2 * fy)) * Mathf.Rad2Deg;
        alignmentCamera.fieldOfView = fovY;

        float aspect = (float)parameters.image_width / parameters.image_height;
        alignmentCamera.aspect = aspect;
    }

    void AlignSMPLX()
    {
        foreach (HumanParams human in parameters.humans)
        {
            GameObject smplxInstance = Instantiate(smplxPrefab);
            SMPLX smplxComponent = smplxInstance.GetComponent<SMPLX>();
            
            Vector3 position = new Vector3(human.translation[0], human.translation[1], human.translation[2]);
            smplxInstance.transform.position = position;

            Vector3 rotationVector = new Vector3(human.rotation_vector[0][0], human.rotation_vector[0][1], human.rotation_vector[0][2]);
            smplxInstance.transform.rotation = Quaternion.Euler(rotationVector * Mathf.Rad2Deg)* Quaternion.Euler(0, 180, 0);

            if (human.rotation_vector != null) ApplyPose(smplxComponent, human.rotation_vector);
            if (human.shape != null) ApplyShape(smplxComponent, human.shape);
            if (human.expression != null) ApplyExpression(smplxComponent, human.expression);
            //if (human.translation_pelvis != null) AdjustPelvisPosition(smplxInstance, human.translation_pelvis[0]);

            // Uncomment and adjust if needed
            if (human.joints_2d != null) AlignWithJoint2D(smplxInstance, human.joints_2d, parameters.image_width, parameters.image_height, human.translation[2]);
        }
    }

    private void ApplyPose(SMPLX smplxMannequin, float[][] newPose) 
    {
        if (newPose.Length != _smplxJointNames.Length)
        {
            Debug.LogError($"Incorrect number of pose values: {newPose.Length}, expected: {_smplxJointNames.Length}");
            return;
        }

        for (int i = 0; i < newPose.Length; i++)
        {
            string jointName = _smplxJointNames[i];
            float rodX = newPose[i][0];
            float rodY = newPose[i][1];
            float rodZ = newPose[i][2];

            Quaternion rotation = SMPLX.QuatFromRodrigues(rodX, rodY, rodZ);
            smplxMannequin.SetLocalJointRotation(jointName, rotation);
        }

        smplxMannequin.EnablePoseCorrectives(true);
        Debug.Log("Pose applied successfully.");
    }

    void ApplyShape(SMPLX smplxMannequin, float[] newBetas)
    {
        if (newBetas.Length != SMPLX.NUM_BETAS)
        {
            Debug.LogError($"Incorrect number of betas: {newBetas.Length}, expected: {SMPLX.NUM_BETAS}");
            return;
        }
        
        for (int i = 0; i < SMPLX.NUM_BETAS; i++)
        {
            smplxMannequin.betas[i] = newBetas[i];
        }
        smplxMannequin.SetBetaShapes();
        Debug.Log("Shape parameters applied successfully.");
    }

    void ApplyExpression(SMPLX smplxMannequin, float[] newExpression) 
    {
        if (newExpression.Length != SMPLX.NUM_EXPRESSIONS)
        {
            Debug.LogError($"Incorrect number of expressions: {newExpression.Length}, expected: {SMPLX.NUM_EXPRESSIONS}");
            return;
        }
        
        for (int i = 0; i < SMPLX.NUM_EXPRESSIONS; i++)
        {
            smplxMannequin.expressions[i] = newExpression[i];
        }
        smplxMannequin.SetExpressions();
        Debug.Log("Expression parameters applied successfully.");
    }

    void AdjustPelvisPosition(GameObject smplxInstance, float[] pelvisTranslation)
    {
        Vector3 pelvisOffset = new Vector3(pelvisTranslation[0], pelvisTranslation[1], pelvisTranslation[2]);
        smplxInstance.transform.position += pelvisOffset;
    }

    void AlignWithJoint2D(GameObject smplxInstance, float[][] joints_2d, float image_width, float image_height, float translationZ)
    {
        Vector2 pelvis2D = new Vector2(joints_2d[0][0], joints_2d[0][1]);
        Vector2 pelvisViewport = new Vector2(pelvis2D.x / image_width, 1 - (pelvis2D.y / image_height));
        Ray ray = alignmentCamera.ViewportPointToRay(new Vector3(pelvisViewport.x, pelvisViewport.y, 0));
        float pelvisDistance = translationZ;
        Vector3 pelvis3DWorld = ray.GetPoint(pelvisDistance);
        Vector3 offset = pelvis3DWorld - smplxInstance.transform.position;
        smplxInstance.transform.position += offset;
    }

    void SetupBackgroundImage()
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.transform.position = new Vector3(0, 0, 10);
        
        float aspectRatio = (float)backgroundImage.width / backgroundImage.height;
        float quadHeight = 2f * Mathf.Tan(alignmentCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * quad.transform.position.z;
        float quadWidth = quadHeight * aspectRatio;
        
        quad.transform.localScale = new Vector3(quadWidth, quadHeight, 1);
        
        Material mat = new Material(Shader.Find("Unlit/Texture"));
        mat.mainTexture = backgroundImage;
        quad.GetComponent<Renderer>().material = mat;
    }
}