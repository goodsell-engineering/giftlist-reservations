using Reservations.Domain.Reservations;

namespace Reservations.UnitTests.Reservations;

public sealed class ReleaseSecretTests
{
    [Fact]
    public void Constructor_ShouldAccept_A32CharacterBase62Value()
    {
        // Arrange
        var value = new string('a', ReleaseSecret.Length);

        // Act
        var secret = new ReleaseSecret(value);

        // Assert
        Assert.Equal(value, secret.Value);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentException_WhenValueIsTooShort()
    {
        // Arrange
        var tooShort = new string('a', ReleaseSecret.Length - 1);

        // Act
        var exception = Record.Exception(() => new ReleaseSecret(tooShort));

        // Assert
        var argumentException = Assert.IsType<ArgumentException>(exception);
        Assert.Equal("value", argumentException.ParamName);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentException_WhenValueIsTooLong()
    {
        // Arrange
        var tooLong = new string('a', ReleaseSecret.Length + 1);

        // Act
        var exception = Record.Exception(() => new ReleaseSecret(tooLong));

        // Assert
        Assert.IsType<ArgumentException>(exception);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentException_WhenValueContainsANonBase62Character()
    {
        // Arrange
        var withHyphen = new string('a', ReleaseSecret.Length - 1) + "-";

        // Act
        var exception = Record.Exception(() => new ReleaseSecret(withHyphen));

        // Assert
        Assert.IsType<ArgumentException>(exception);
    }

    [Fact]
    public void Constructor_ShouldThrowArgumentException_WhenValueIsWhitespace()
    {
        // Arrange — none

        // Act
        var exception = Record.Exception(() => new ReleaseSecret("   "));

        // Assert
        Assert.IsType<ArgumentException>(exception);
    }

    [Fact]
    public void Equals_ShouldReturnTrue_ForTwoSecretsWithTheSameValue()
    {
        // Arrange
        var value = new string('b', ReleaseSecret.Length);
        var first = new ReleaseSecret(value);
        var second = new ReleaseSecret(value);

        // Act
        var equal = first.Equals(second);

        // Assert
        Assert.True(equal);
    }

    [Fact]
    public void ToString_ShouldNotExposeTheValue_SoAStrayLogStatementCannotLeakIt()
    {
        // Arrange
        var secret = new ReleaseSecret(new string('z', ReleaseSecret.Length));

        // Act
        var text = secret.ToString();

        // Assert
        Assert.DoesNotContain('z', text);
    }
}
