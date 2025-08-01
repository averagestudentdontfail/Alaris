//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class Fdm6dimSolver : global::System.IDisposable {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  private bool swigCMemOwnBase;

  internal Fdm6dimSolver(global::System.IntPtr cPtr, bool cMemoryOwn) {
    swigCMemOwnBase = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(Fdm6dimSolver obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  ~Fdm6dimSolver() {
    Dispose(false);
  }

  public void Dispose() {
    Dispose(true);
    global::System.GC.SuppressFinalize(this);
  }

  protected virtual void Dispose(bool disposing) {
    lock(this) {
      if (swigCPtr.Handle != global::System.IntPtr.Zero) {
        if (swigCMemOwnBase) {
          swigCMemOwnBase = false;
          NQuantLibcPINVOKE.delete_Fdm6dimSolver(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
    }
  }

  public Fdm6dimSolver(FdmSolverDesc solverDesc, FdmSchemeDesc schemeDesc, FdmLinearOpComposite op) : this(NQuantLibcPINVOKE.new_Fdm6dimSolver(FdmSolverDesc.getCPtr(solverDesc), FdmSchemeDesc.getCPtr(schemeDesc), FdmLinearOpComposite.getCPtr(op)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public double interpolateAt(DoubleVector x) {
    double ret = NQuantLibcPINVOKE.Fdm6dimSolver_interpolateAt(swigCPtr, DoubleVector.getCPtr(x));
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public double thetaAt(DoubleVector x) {
    double ret = NQuantLibcPINVOKE.Fdm6dimSolver_thetaAt(swigCPtr, DoubleVector.getCPtr(x));
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

}
