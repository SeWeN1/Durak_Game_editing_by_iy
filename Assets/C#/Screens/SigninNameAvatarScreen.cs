using JSON_server;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

//�����������
public class SigninNameAvatarScreen : BaseScreen
{
    //��������� ����
    public InputField m_name;
    public InputField m_email;
    public InputField m_password;

    [Header("Message")] //��������� ������ Toast
    public GameObject MessageScreen;
    public TMP_Text MessageText;

    void Start()
    {
        //���������� ��������� ����������� � �����
        SocketNetwork.SignInSucsessed += SigninSuccessed;
        SocketNetwork.error += PrintMaessage;
    }

    //�������� �����������
    private void SigninSuccessed()
    {
        MainThreadDispatcher.RunOnMainThread(() =>
        {
            m_screenDirector.ActiveScreen(EScreens.LoginScreen);
            Debug.Log("SigninSuccessed");
        });
    }

    //��������� ������� ������������������ (������� �� ������)
    public void SigninClickHandler()
    {
        if (!data_validator.CheckEmail(m_email.text))
        {
            PrintMaessage("incorrect email type");
            return;
        }
        if (!data_validator.CheckPassword(m_password.text))
        {
            PrintMaessage("incorrect password type");
            return;
        }

        m_socketNetwork.Emit_signIn(m_name.text, m_email.text, m_password.text);
    }

    //���������� ������ Toast � Android
    public void PrintMaessage(string Message)
    {
        MainThreadDispatcher.RunOnMainThread(() =>
        {
            MessageText.text = Message;
            LeanTween.scale(MessageScreen, new Vector3(1, 1, 1), 2).setOnComplete(finishMessage);
        });
    }
    public void finishMessage()
    {
        MainThreadDispatcher.RunOnMainThread(() =>
        {
            LeanTween.scale(MessageScreen, new Vector3(0, 0, 0), 1).setOnComplete(finishMessage);
        });
    }
}
