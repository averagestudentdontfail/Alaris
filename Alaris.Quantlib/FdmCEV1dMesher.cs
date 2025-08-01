//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class FdmCEV1dMesher : Fdm1dMesher {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  private bool swigCMemOwnDerived;

  internal FdmCEV1dMesher(global::System.IntPtr cPtr, bool cMemoryOwn) : base(NQuantLibcPINVOKE.FdmCEV1dMesher_SWIGSmartPtrUpcast(cPtr), true) {
    swigCMemOwnDerived = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(FdmCEV1dMesher obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  protected override void Dispose(bool disposing) {
    lock(this) {
      if (swigCPtr.Handle != global::System.IntPtr.Zero) {
        if (swigCMemOwnDerived) {
          swigCMemOwnDerived = false;
          NQuantLibcPINVOKE.delete_FdmCEV1dMesher(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
      base.Dispose(disposing);
    }
  }

  public FdmCEV1dMesher(uint size, double f0, double alpha, double beta, double maturity, double eps, double scaleFactor, DoublePair cPoint) : this(NQuantLibcPINVOKE.new_FdmCEV1dMesher__SWIG_0(size, f0, alpha, beta, maturity, eps, scaleFactor, DoublePair.getCPtr(cPoint)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FdmCEV1dMesher(uint size, double f0, double alpha, double beta, double maturity, double eps, double scaleFactor) : this(NQuantLibcPINVOKE.new_FdmCEV1dMesher__SWIG_1(size, f0, alpha, beta, maturity, eps, scaleFactor), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FdmCEV1dMesher(uint size, double f0, double alpha, double beta, double maturity, double eps) : this(NQuantLibcPINVOKE.new_FdmCEV1dMesher__SWIG_2(size, f0, alpha, beta, maturity, eps), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FdmCEV1dMesher(uint size, double f0, double alpha, double beta, double maturity) : this(NQuantLibcPINVOKE.new_FdmCEV1dMesher__SWIG_3(size, f0, alpha, beta, maturity), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

}
