using UnityEngine;

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

            // Set up background image
            SetupBackgroundImage();
        }
    }

    void SetupBackgroundImage()
    {
        GameObject quad = GameObject.CreatePrimitive(PrimitiveType.Quad);
        quad.transform.position = new Vector3(0, 0, 10); // Ajusta esto según sea necesario
        
        float aspectRatio = (float)backgroundImage.width / backgroundImage.height;
        float quadHeight = 2f * Mathf.Tan(alignmentCamera.fieldOfView * 0.5f * Mathf.Deg2Rad) * quad.transform.position.z;
        float quadWidth = quadHeight * aspectRatio;
        
        quad.transform.localScale = new Vector3(quadWidth, quadHeight, 1);
        
        Material mat = new Material(Shader.Find("Unlit/Texture"));
        mat.mainTexture = backgroundImage;
        quad.GetComponent<Renderer>().material = mat;
    }

    void SetupCamera()
    {
        float fx = parameters.camera_intrinsics[0];
        float fy = parameters.camera_intrinsics[4];
        float cx = parameters.camera_intrinsics[2];
        float cy = parameters.camera_intrinsics[5];

        float fovY = 2 * Mathf.Atan(parameters.image_height / (2 * fy)) * Mathf.Rad2Deg;
        alignmentCamera.fieldOfView = fovY;

        float aspect = (float)parameters.image_width / parameters.image_height;
        alignmentCamera.aspect = aspect;

        // Adjust camera position if needed
        // alignmentCamera.transform.position = ...
    }

    void AlignSMPLX()
    {
        foreach (HumanParams human in parameters.humans)
        {
            GameObject smplxInstance = Instantiate(smplxPrefab);
            SMPLX smplxComponent = smplxInstance.GetComponent<SMPLX>();
            
            // Set position
            Vector3 position = new Vector3(
                human.translation[0],
                human.translation[1],
                human.translation[2]
            );
            smplxInstance.transform.position = position;

            // Set rotation
            Vector3 rotationVector = new Vector3(
                human.rotation_vector[0],
                human.rotation_vector[1],
                human.rotation_vector[2]
            );
            smplxInstance.transform.rotation = Quaternion.Euler(rotationVector * Mathf.Rad2Deg);

            // Apply pose parameters
            if (human.rotation_vector != null) ApplyPose(smplxComponent, human.rotation_vector);

            // Apply shape parameters
            if (human.shape != null) ApplyShape(smplxComponent, human.shape);

            // Apply expression parameters
            if (human.expression != null) ApplyExpression(smplxComponent, human.expression);

            // Apply shape parameters
            if (human.shape != null) ApplyShape(smplxComponent, human.shape);

            // Adjust pelvis position if needed
            if (human.translation_pelvis != null) AdjustPelvisPosition(smplxInstance, human.translation_pelvis);

            // Align with joints2D
            //if (human.joints_2d != null) AlignWithJoint2D(smplxInstance, human.joints_2d, parameters.image_width, parameters.image_height, human.translation[2]);
        }
    }

    private void ApplyPose(SMPLX smplxMannequin, float[] newPose) 
    {
        //Debug.Log("El largo de las poses es de: " + newPose.Length);

        if (newPose.Length != (_smplxJointNames.Length+1)*3)
        {
            Debug.LogError("No tiene el número correcto de valores: " + newPose.Length + " porque deberían ser: " + _smplxJointNames.Length);
            return;
        }

        for (int i = 1; i < (_smplxJointNames.Length+1); i++)
        {
            string jointName = _smplxJointNames[i-1];
            float rodX = newPose[i*3];
            float rodY = newPose[i*3+1];
            float rodZ = newPose[i*3+2];

            // Convertir de notación de Rodrigues a cuaternión
            Quaternion rotation = SMPLX.QuatFromRodrigues(rodX, rodY, rodZ);

            // Aplicar la rotación al joint
            smplxMannequin.SetLocalJointRotation(jointName, rotation);
        }

        // Actualizar las posiciones de los joints y los correctivos de pose
        //smplxMannequin.UpdateJointPositions();
        //smplxMannequin.UpdatePoseCorrectives();
        smplxMannequin.EnablePoseCorrectives(true);

        Debug.Log("Se ha aplicado correctamente la pose.");
    }

    void ApplyShape(SMPLX smplxMannequin, float[] newBetas)
    {
        if (newBetas.Length != SMPLX.NUM_BETAS)
        {
            Debug.LogError("No tiene el número correcto de betas: " + newBetas.Length + " porque deberían ser: " + SMPLX.NUM_BETAS);
            return;
        }
        
        for (int i = 0; i < SMPLX.NUM_BETAS; i++)
        {
            smplxMannequin.betas[i] = newBetas[i];
            //Debug.Log("newBetas[" + i + "]: " + newBetas[i]);
        }
        smplxMannequin.SetBetaShapes();

        Debug.Log("Se han aplicado correctamente los blendshapes.");
    }

    void ApplyExpression(SMPLX smplxMannequin, float[] newExpression) 
    {
        if (newExpression.Length != SMPLX.NUM_EXPRESSIONS)
        {
            Debug.LogError("No tiene el número correcto de expresiones: " + newExpression.Length + " porque deberían ser: " + SMPLX.NUM_EXPRESSIONS);
            return;
        }
        
        for (int i = 0; i < SMPLX.NUM_EXPRESSIONS; i++)
        {
            smplxMannequin.expressions[i] = newExpression[i];
            //Debug.Log("newExpression[" + i + "]: " + newExpression[i]);
        }
        smplxMannequin.SetExpressions();

        Debug.Log("Se han aplicado correctamente las expresiones.");
    }

    void AdjustPelvisPosition(GameObject smplxInstance, float[] pelvisTranslation)
    {
        Vector3 pelvisOffset = new Vector3(
            pelvisTranslation[0],
            pelvisTranslation[1],
            pelvisTranslation[2]
        );
        
        // Adjust the position of the pelvis or the entire model if needed
        // This depends on how your SMPLX model is structured in Unity
        smplxInstance.transform.position += pelvisOffset;
    }

    void AlignWithJoint2D(GameObject smplxInstance, float[] joints_2d, float image_width, float image_height, float translationZ)
    {
        // Align using pelvis 2D joint
        Vector2 pelvis2D = new Vector2(joints_2d[0], joints_2d[1]);
        Vector2 pelvisViewport = new Vector2(pelvis2D.x / image_width, 
                                             1 - (pelvis2D.y / image_height));
        Ray ray = alignmentCamera.ViewportPointToRay(new Vector3(pelvisViewport.x, pelvisViewport.y, 0));
        float pelvisDistance = translationZ;
        Vector3 pelvis3DWorld = ray.GetPoint(pelvisDistance);
        Vector3 offset = pelvis3DWorld - smplxInstance.transform.position;
        smplxInstance.transform.position += offset;
    }
}