using UnityEngine;

public class PlayerController : MonoBehaviour
{
    public float _speedMove;
    public float _jumpPower;

    private float _gravityForce;
    private Vector3 _moveVector;

    private CharacterController _chController;
    private Animator _chAnimator;

    public Transform _cameraTransform;

    private void Start()
    {
        _chController = GetComponent<CharacterController>();
        _chAnimator = GetComponent<Animator>();

        if (_cameraTransform == null && Camera.main != null)
            _cameraTransform = Camera.main.transform;
    }

    private void Update()
    {
        CharacterMove();
    }

    private void CharacterMove()
    {
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        Vector3 _camForward = _cameraTransform.forward;
        _camForward.y = 0;
        _camForward.Normalize();

        Vector3 _camRight = _cameraTransform.right;
        _camRight.y = 0;
        _camRight.Normalize();

        _moveVector = _camForward * v + _camRight * h;
        _moveVector.Normalize();
        _moveVector *= _speedMove;

        _chAnimator.SetBool("Move", _moveVector.magnitude > 0);

        if (_moveVector.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(_moveVector);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
        }

        _gravityForce += Physics.gravity.y * Time.deltaTime;
        _moveVector.y = _gravityForce;

        _chController.Move(_moveVector * Time.deltaTime);

        if (_chController.isGrounded) _gravityForce = -0.5f;
    }

    // ВАЖНО: считаем движение только по горизонтали (XZ), чтобы гравитация не считалась «движением»
    public bool IsMoving()
    {
        Vector3 horizontal = _moveVector;
        horizontal.y = 0f;
        return horizontal.sqrMagnitude > 0.01f;
    }

    private void GamingGravity()
    {
        if (!_chController.isGrounded)
        {
            _gravityForce -= 20f * Time.deltaTime;
        }
        else
        {
            _gravityForce = -1f;
        }
        if (Input.GetKeyDown(KeyCode.Space) && _chController.isGrounded)
        {
            _gravityForce = _jumpPower;
        }
    }
}
