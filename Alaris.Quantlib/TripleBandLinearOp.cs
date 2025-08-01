//------------------------------------------------------------------------------
// <auto-generated />
//
// This file was automatically generated by SWIG (https://www.swig.org).
// Version 4.2.0
//
// Do not make changes to this file unless you know what you are doing - modify
// the SWIG interface file instead.
//------------------------------------------------------------------------------


public class TripleBandLinearOp : FdmLinearOp {
  private global::System.Runtime.InteropServices.HandleRef swigCPtr;
  private bool swigCMemOwnDerived;

  internal TripleBandLinearOp(global::System.IntPtr cPtr, bool cMemoryOwn) : base(NQuantLibcPINVOKE.TripleBandLinearOp_SWIGSmartPtrUpcast(cPtr), true) {
    swigCMemOwnDerived = cMemoryOwn;
    swigCPtr = new global::System.Runtime.InteropServices.HandleRef(this, cPtr);
  }

  internal static global::System.Runtime.InteropServices.HandleRef getCPtr(TripleBandLinearOp obj) {
    return (obj == null) ? new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero) : obj.swigCPtr;
  }

  protected override void Dispose(bool disposing) {
    lock(this) {
      if (swigCPtr.Handle != global::System.IntPtr.Zero) {
        if (swigCMemOwnDerived) {
          swigCMemOwnDerived = false;
          NQuantLibcPINVOKE.delete_TripleBandLinearOp(swigCPtr);
        }
        swigCPtr = new global::System.Runtime.InteropServices.HandleRef(null, global::System.IntPtr.Zero);
      }
      base.Dispose(disposing);
    }
  }

  public TripleBandLinearOp(uint direction, FdmMesher mesher) : this(NQuantLibcPINVOKE.new_TripleBandLinearOp(direction, FdmMesher.getCPtr(mesher)), true) {
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public override QlArray apply(QlArray r) {
    QlArray ret = new QlArray(NQuantLibcPINVOKE.TripleBandLinearOp_apply(swigCPtr, QlArray.getCPtr(r)), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public QlArray solve_splitting(QlArray r, double a, double b) {
    QlArray ret = new QlArray(NQuantLibcPINVOKE.TripleBandLinearOp_solve_splitting__SWIG_0(swigCPtr, QlArray.getCPtr(r), a, b), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public QlArray solve_splitting(QlArray r, double a) {
    QlArray ret = new QlArray(NQuantLibcPINVOKE.TripleBandLinearOp_solve_splitting__SWIG_1(swigCPtr, QlArray.getCPtr(r), a), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public TripleBandLinearOp mult(QlArray u) {
    TripleBandLinearOp ret = new TripleBandLinearOp(NQuantLibcPINVOKE.TripleBandLinearOp_mult(swigCPtr, QlArray.getCPtr(u)), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public TripleBandLinearOp multR(QlArray u) {
    TripleBandLinearOp ret = new TripleBandLinearOp(NQuantLibcPINVOKE.TripleBandLinearOp_multR(swigCPtr, QlArray.getCPtr(u)), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public TripleBandLinearOp add(TripleBandLinearOp m) {
    TripleBandLinearOp ret = new TripleBandLinearOp(NQuantLibcPINVOKE.TripleBandLinearOp_add__SWIG_0(swigCPtr, TripleBandLinearOp.getCPtr(m)), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public TripleBandLinearOp add(QlArray u) {
    TripleBandLinearOp ret = new TripleBandLinearOp(NQuantLibcPINVOKE.TripleBandLinearOp_add__SWIG_1(swigCPtr, QlArray.getCPtr(u)), true);
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
    return ret;
  }

  public void axpyb(QlArray a, TripleBandLinearOp x, TripleBandLinearOp y, QlArray b) {
    NQuantLibcPINVOKE.TripleBandLinearOp_axpyb(swigCPtr, QlArray.getCPtr(a), TripleBandLinearOp.getCPtr(x), TripleBandLinearOp.getCPtr(y), QlArray.getCPtr(b));
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

  public void swap(TripleBandLinearOp m) {
    NQuantLibcPINVOKE.TripleBandLinearOp_swap(swigCPtr, TripleBandLinearOp.getCPtr(m));
    if (NQuantLibcPINVOKE.SWIGPendingException.Pending) throw NQuantLibcPINVOKE.SWIGPendingException.Retrieve();
  }

}
