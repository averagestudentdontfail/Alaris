//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class FdmMesherComposite : FdmMesher {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  private bool swigCMemOwnDerived;

  internal FdmMesherComposite(global::System.IntPtr cPtr, bool cMemoryOwn) : base(NQuantLibcPINVOKE.FdmMesherComposite_SWIGSmartPtrUpcast(cPtr), true) {
    swigCMemOwnDerived = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(FdmMesherComposite obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  protected override void Dispose(bool disposing) {
    lock(this) {
      if (swigCPtr.Handle != global::System.IntPtr.Zero) {
        if (swigCMemOwnDerived) {
          swigCMemOwnDerived = false;
          NQuantLibcPINVOKE.delete_FdmMesherComposite(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
      base.Dispose(disposing);
    }
  }

  public FdmMesherComposite(FdmLinearOpLayout layout, Fdm1dMesherVector mesher) : this(NQuantLibcPINVOKE.new_FdmMesherComposite__SWIG_0(FdmLinearOpLayout.getCPtr(layout), Fdm1dMesherVector.getCPtr(mesher)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FdmMesherComposite(Fdm1dMesherVector mesher) : this(NQuantLibcPINVOKE.new_FdmMesherComposite__SWIG_1(Fdm1dMesherVector.getCPtr(mesher)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FdmMesherComposite(Fdm1dMesher mesher) : this(NQuantLibcPINVOKE.new_FdmMesherComposite__SWIG_2(Fdm1dMesher.getCPtr(mesher)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FdmMesherComposite(Fdm1dMesher m1, Fdm1dMesher m2) : this(NQuantLibcPINVOKE.new_FdmMesherComposite__SWIG_3(Fdm1dMesher.getCPtr(m1), Fdm1dMesher.getCPtr(m2)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FdmMesherComposite(Fdm1dMesher m1, Fdm1dMesher m2, Fdm1dMesher m3) : this(NQuantLibcPINVOKE.new_FdmMesherComposite__SWIG_4(Fdm1dMesher.getCPtr(m1), Fdm1dMesher.getCPtr(m2), Fdm1dMesher.getCPtr(m3)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public FdmMesherComposite(Fdm1dMesher m1, Fdm1dMesher m2, Fdm1dMesher m3, Fdm1dMesher m4) : this(NQuantLibcPINVOKE.new_FdmMesherComposite__SWIG_5(Fdm1dMesher.getCPtr(m1), Fdm1dMesher.getCPtr(m2), Fdm1dMesher.getCPtr(m3), Fdm1dMesher.getCPtr(m4)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public Fdm1dMesherVector getFdm1dMeshers() {
    Fdm1dMesherVector ret = new Fdm1dMesherVector(NQuantLibcPINVOKE.FdmMesherComposite_getFdm1dMeshers(swigCPtr), false);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

}
