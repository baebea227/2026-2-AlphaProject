using UnityEngine;

/// <summary>
/// 세션 이름으로 사용할 숫자 6자리 룸 코드의 생성, 정리, 검증을 담당합니다.
/// </summary>
public static class RoomCodeUtility
{
    public const int CodeLength = 6;

    private const int MinimumCode = 100000;
    private const int MaximumCodeExclusive = 1000000;

    /// <summary>
    /// 첫 자리가 0이 아닌 숫자 6자리 룸 코드를 생성합니다.
    /// </summary>
    public static string Generate()
    {
        return Random.Range(MinimumCode, MaximumCodeExclusive).ToString();
    }

    /// <summary>
    /// 입력값에서 숫자만 추출하고 최대 6자리로 제한합니다.
    /// </summary>
    public static string Normalize(string roomCode)
    {
        if (string.IsNullOrEmpty(roomCode))
        {
            return string.Empty;
        }

        char[] digits = new char[CodeLength];
        int digitCount = 0;

        for (int index = 0; index < roomCode.Length && digitCount < CodeLength; index++)
        {
            char character = roomCode[index];
            if (character >= '0' && character <= '9')
            {
                digits[digitCount] = character;
                digitCount++;
            }
        }

        return new string(digits, 0, digitCount);
    }

    /// <summary>
    /// 입력값이 정확히 숫자 6자리인지 확인합니다.
    /// </summary>
    public static bool IsValid(string roomCode)
    {
        if (roomCode == null || roomCode.Length != CodeLength)
        {
            return false;
        }

        for (int index = 0; index < roomCode.Length; index++)
        {
            char character = roomCode[index];
            if (character < '0' || character > '9')
            {
                return false;
            }
        }

        return true;
    }
}
