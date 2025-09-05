using MediatR;
using AutoMapper;
using Contracts.DTOs;
using Contracts.Models;
using DAL.Repositories;
using API.Services;

namespace API.Features.Scripts.Commands.CreateScript
{
    /// <summary>
    /// Handler for creating new pipeline stage scripts
    /// </summary>
    public class CreateScriptHandler : IRequestHandler<CreateScriptCommand, PipelineStageScriptDto>
    {
        private readonly IPipelineStageScriptRepository _scriptRepository;
        private readonly IProductRepository _productRepository;
        private readonly IPipelineStageRepository _stageRepository;
        private readonly ISqlScriptValidatorService _validatorService;
        private readonly IMapper _mapper;

        public CreateScriptHandler(
            IPipelineStageScriptRepository scriptRepository,
            IProductRepository productRepository,
            IPipelineStageRepository stageRepository,
            ISqlScriptValidatorService validatorService,
            IMapper mapper)
        {
            _scriptRepository = scriptRepository ?? throw new ArgumentNullException(nameof(scriptRepository));
            _productRepository = productRepository ?? throw new ArgumentNullException(nameof(productRepository));
            _stageRepository = stageRepository ?? throw new ArgumentNullException(nameof(stageRepository));
            _validatorService = validatorService ?? throw new ArgumentNullException(nameof(validatorService));
            _mapper = mapper ?? throw new ArgumentNullException(nameof(mapper));
        }

        public async Task<PipelineStageScriptDto> Handle(CreateScriptCommand request, CancellationToken cancellationToken)
        {
            // Validate product exists
            var product = await _productRepository.GetByIdAsync(request.ProductId);
            if (product == null)
            {
                throw new ArgumentException($"Product with ID {request.ProductId} not found");
            }

            // Validate pipeline stage exists
            var stage = await _stageRepository.GetByIdAsync(request.PipelineStageId);
            if (stage == null)
            {
                throw new ArgumentException($"Pipeline stage with ID {request.PipelineStageId} not found");
            }

            // Validate script name uniqueness
            if (await _scriptRepository.ExistsAsync(request.ProductId, request.PipelineStageId, request.Name))
            {
                throw new InvalidOperationException($"Script with name '{request.Name}' already exists for this product and stage");
            }

            // Validate failure action
            if (!FailureActions.IsValid(request.OnFailureAction))
            {
                throw new ArgumentException($"Invalid failure action: {request.OnFailureAction}. Valid actions are: {string.Join(", ", FailureActions.All)}");
            }

            // Validate expected result type
            if (!ScriptResultTypes.IsValid(request.ExpectedResultType))
            {
                throw new ArgumentException($"Invalid result type: {request.ExpectedResultType}. Valid types are: {string.Join(", ", ScriptResultTypes.All)}");
            }

            // Perform security validation
            var validationResult = await _validatorService.ValidateScriptAsync(request.SqlScript);
            
            // Get execution order if not specified
            var executionOrder = request.ExecutionOrder ?? await _scriptRepository.GetNextExecutionOrderAsync(request.ProductId, request.PipelineStageId);

            // Create script entity
            var script = new PipelineStageScript
            {
                Id = Guid.NewGuid(),
                ProductId = request.ProductId,
                PipelineStageId = request.PipelineStageId,
                Name = request.Name,
                Description = request.Description,
                SqlScript = request.SqlScript,
                ExecutionOrder = executionOrder,
                IsEnabled = request.IsEnabled,
                TimeoutMinutes = request.TimeoutMinutes,
                OnFailureAction = request.OnFailureAction,
                RetryAttempts = request.RetryAttempts,
                RetryDelaySeconds = request.RetryDelaySeconds,
                ExpectedResultType = request.ExpectedResultType,
                ExpectedMinRows = request.ExpectedMinRows,
                ExpectedMaxRows = request.ExpectedMaxRows,
                Tags = request.Tags,
                IsSecurityValidated = validationResult.IsValid,
                SecurityValidationNotes = validationResult.IsValid ? "Passed automated security validation" : 
                    string.Join("; ", validationResult.SecurityIssues.Select(i => i.Description)),
                LastValidatedAt = validationResult.IsValid ? DateTime.UtcNow : null,
                CreatedBy = request.CreatedBy,
                CreatedAt = DateTime.UtcNow,
                UpdatedAt = DateTime.UtcNow
            };

            // Save to database
            var created = await _scriptRepository.CreateAsync(script);

            // Map to DTO
            var dto = _mapper.Map<PipelineStageScriptDto>(created);
            dto.ProductName = product.Name;
            dto.PipelineStage = _mapper.Map<PipelineStageDto>(stage);

            return dto;
        }
    }
}