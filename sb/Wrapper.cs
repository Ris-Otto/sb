using System.Text;

namespace sb;

public class Wrapper
{
    public StringBuilder strBuild { get; }
    public int count { get; private set; }
    public Wrapper(){
        strBuild = new StringBuilder();
    }
    public Wrapper AppendLine(string? toAppendParam = null){
        strBuild.AppendLine(toAppendParam);
        count++;
        return this;
    }

    public override string ToString() {
        return strBuild.ToString();
    }
}