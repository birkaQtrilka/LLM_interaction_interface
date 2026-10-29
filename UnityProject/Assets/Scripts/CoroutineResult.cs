public class CoroutineResult<Res, Err>
{
    public ContextStatus Status;
    public Res Response;
    public Err Error;
    public bool IsDone;

    public void SetResult(Res r)
    {
        Status = ContextStatus.Success;
        Response = r;
        IsDone = true;
    } 

    public void SetError(Err e)
    {
        Status = ContextStatus.Failure;
        Error = e;
        IsDone = true;
    }
}
