$ErrorActionPreference='Stop'
$root=Split-Path $PSScriptRoot
$text=[IO.File]::ReadAllText((Join-Path $root 'api/source/learning-tracking.asyncapi.yaml')).Replace("`r`n","`n")
$channels=@'
  lessonParticipantsChanged:
    address: vairas.scheduling.lesson-participants-changed.v1
    messages:
      LessonParticipantsChanged:
        $ref: '#/components/messages/LessonParticipantsChanged'
  schoolLearningAuthorityChanged:
    address: vairas.school.learning-authority-changed.v1
    messages:
      SchoolLearningAuthorityChanged:
        $ref: '#/components/messages/SchoolLearningAuthorityChanged'
  trainingEvidenceInvalidated:
    address: vairas.learning.training-evidence-invalidated.v1
    messages:
      TrainingEvidenceInvalidated:
        $ref: '#/components/messages/TrainingEvidenceInvalidated'
  assessmentResultRevised:
    address: vairas.learning.assessment-result-revised.v1
    messages:
      AssessmentResultRevised:
        $ref: '#/components/messages/AssessmentResultRevised'
'@
$operations=@'
  consumeLessonParticipantsChanged:
    action: receive
    channel:
      $ref: '#/channels/lessonParticipantsChanged'
  consumeSchoolLearningAuthorityChanged:
    action: receive
    channel:
      $ref: '#/channels/schoolLearningAuthorityChanged'
  publishTrainingEvidenceInvalidated:
    action: send
    channel:
      $ref: '#/channels/trainingEvidenceInvalidated'
  publishAssessmentResultRevised:
    action: send
    channel:
      $ref: '#/channels/assessmentResultRevised'
'@
$schemas=@'
    LessonParticipantsChangedPayload:
      type: object
      additionalProperties: false
      required: [schoolId, lessonId, lessonKind, teacherId, groupId, groupMembershipRevision]
      properties:
        schoolId: {type: string, format: uuid}
        lessonId: {type: string, format: uuid}
        lessonKind: {type: string, const: Theory}
        teacherId: {type: string, format: uuid}
        groupId: {type: string, format: uuid}
        groupMembershipRevision: {type: integer, minimum: 1}
    SchoolLearningAuthorityChangedPayload:
      type: object
      additionalProperties: false
      required: [schoolId, schoolRevision, changeKind, effectiveAt]
      properties:
        schoolId: {type: string, format: uuid}
        schoolRevision: {type: integer, minimum: 1}
        changeKind: {type: string, minLength: 1}
        effectiveAt: {type: string, format: date-time}
    TrainingEvidenceInvalidatedPayload:
      type: object
      additionalProperties: false
      required: [schoolId, enrollmentId, dependencyRevision, reason]
      properties:
        schoolId: {type: string, format: uuid}
        enrollmentId: {type: string, format: uuid}
        dependencyRevision: {type: integer, minimum: 1}
        reason: {type: string, minLength: 1}
    AssessmentResultRevisedPayload:
      type: object
      additionalProperties: false
      required: [schoolId, enrollmentId, assessmentId, version, status]
      properties:
        schoolId: {type: string, format: uuid}
        enrollmentId: {type: string, format: uuid}
        assessmentId: {type: string, format: uuid}
        version: {type: integer, minimum: 2}
        status: {type: string, enum: [Recorded, ReviewRequired, Annulled]}
'@
$messages=''
foreach($name in @('LessonParticipantsChanged','SchoolLearningAuthorityChanged','TrainingEvidenceInvalidated','AssessmentResultRevised')){
    $messages+=@"
    ${name}:
      name: $name
      title: $name v1
      contentType: application/json
      headers:
        `$ref: '#/components/schemas/IntegrationHeaders'
      payload:
        `$ref: '#/components/schemas/${name}Payload'
      correlationId:
        location: '`$message.header#/correlationId'

"@
}
$text=$text.Replace("channels:`n","channels:`n"+$channels.Replace("`r`n","`n")+"`n")
$text=$text.Replace("operations:`n","operations:`n"+$operations.Replace("`r`n","`n")+"`n")
$text=$text.Replace("  schemas:`n","  schemas:`n"+$schemas.Replace("`r`n","`n")+"`n")
$text=$text.Replace("  messages:`n    AssessmentFailed:","  messages:`n"+$messages.Replace("`r`n","`n")+"    AssessmentFailed:")
$text=$text.Replace('PROPOSED AsyncAPI 3.0.0 contract for Learning Tracking.','Implemented Learning Tracking SDK/Outbox contract; local verification and deployment are separate.').Replace('This is a proposed inter-service','This is a versioned inter-service').Replace('not a claim of implemented integration.','not a claim of production deployment.').Replace('Base DDD event; integration payload proposed','Implemented message shape; deployment unverified')
[IO.File]::WriteAllText((Join-Path $root 'api/learning-tracking.asyncapi.yaml'),$text,[Text.UTF8Encoding]::new($false))
