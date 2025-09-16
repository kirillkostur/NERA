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
        // Получаем ввод
        float h = Input.GetAxis("Horizontal");
        float v = Input.GetAxis("Vertical");

        // Берем направления камеры по XZ плоскости
        Vector3 _camForward = _cameraTransform.forward;
        _camForward.y = 0;
        _camForward.Normalize();

        Vector3 _camRight = _cameraTransform.right;
        _camRight.y = 0;
        _camRight.Normalize();

        // Движение относительно камеры
        _moveVector = _camForward * v + _camRight * h;
        _moveVector.Normalize();
        _moveVector *= _speedMove;

        // Анимация
        _chAnimator.SetBool("Move", _moveVector.magnitude > 0);

        // Поворот персонажа к направлению движения
        if (_moveVector.sqrMagnitude > 0.01f)
        {
            Quaternion targetRotation = Quaternion.LookRotation(_moveVector);
            transform.rotation = Quaternion.Slerp(transform.rotation, targetRotation, Time.deltaTime * 10f);
        }

        // Применяем гравитацию
        _gravityForce += Physics.gravity.y * Time.deltaTime;
        _moveVector.y = _gravityForce;

        // Двигаем контроллер
        _chController.Move(_moveVector * Time.deltaTime);

        // Если стоим на земле — сбрасываем гравитацию
        if (_chController.isGrounded) _gravityForce = -0.5f;
    }
    public bool IsMoving()
    {
        return _moveVector.sqrMagnitude > 0.01f;
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
