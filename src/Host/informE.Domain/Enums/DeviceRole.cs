namespace informE.Domain.Enums;

// A tela de Grupos separa "COMPUTADOR DO PROFESSOR" (badge + "acesso
// privilegiado · responsável pelo laboratório") de "COMPUTADORES DOS ALUNOS".
//
// Não vem no enroll: o agente não sabe o papel da máquina. O admin designa
// depois pela UI — ver Device.AssignRole().
public enum DeviceRole { Aluno, Professor }
